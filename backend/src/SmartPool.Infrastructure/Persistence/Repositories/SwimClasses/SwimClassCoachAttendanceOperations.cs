using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RecordClassAttendance;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<ClassAttendanceResponse> RecordClassAttendanceAsync(RecordClassAttendanceCommand request,
        Guid coachUserId, byte[]? image, string? format, CancellationToken cancellationToken)
    {
        string? uploadedPublicId = null;
        string? uploadedUrl = null;
        if (image is not null)
        {
            using var stream = new MemoryStream(image, writable: false);
            var uploaded = await cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription($"attendance.{format}", stream),
                Folder = "smartpool/class-attendances",
                PublicId = Guid.NewGuid().ToString("N"),
                Overwrite = false
            });
            if (uploaded.Error is not null || uploaded.SecureUrl is null || string.IsNullOrWhiteSpace(uploaded.PublicId))
                throw new InvalidOperationException(uploaded.Error?.Message ?? "Không thể lưu ảnh điểm danh.");
            uploadedPublicId = uploaded.PublicId;
            uploadedUrl = uploaded.SecureUrl.AbsoluteUri;
        }

        var committed = false;
        string? previousImageUrl = null;
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await LockClassAsync(request.ClassId, cancellationToken);
            var session = await context.ClassSessions.Include(item => item.Class)
                .SingleOrDefaultAsync(item => item.Id == request.SessionId && item.ClassId == request.ClassId,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy buổi học của lớp.");
            if (session.CoachUserId != coachUserId)
                throw new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được điểm danh.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (session.Class.Status != "Active" || session.Status != "Scheduled")
                throw new SwimClassStateConflictException("Lớp hoặc buổi học không còn cho phép điểm danh.");
            if (request.Status == "Absent" && now <= session.EndsAt)
                throw new SwimClassStateConflictException("Chỉ đánh dấu vắng sau khi buổi học kết thúc.");
            var registration = await context.ClassRegistrations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.RegistrationId && item.ClassId == request.ClassId,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy học viên của lớp.");
            if (registration.Status != "Confirmed")
                throw new SwimClassStateConflictException("Chỉ điểm danh học viên đã được xác nhận tham gia lớp.");
            if (registration.ConfirmedAt > session.EndsAt)
                throw new SwimClassStateConflictException("Học viên chưa tham gia lớp ở buổi học này.");
            var attendance = await context.ClassAttendances.SingleOrDefaultAsync(item =>
                item.SessionId == request.SessionId && item.RegistrationId == request.RegistrationId,
                cancellationToken);
            if ((attendance is null && request.ExpectedUpdatedAt is not null) ||
                (attendance is not null && attendance.UpdatedAt != request.ExpectedUpdatedAt?.UtcDateTime))
                throw new SwimClassStateConflictException("Điểm danh đã thay đổi. Vui lòng tải lại danh sách.");
            if (request.Status == "Verified" && uploadedUrl is null &&
                (attendance?.ImageUrl is null || attendance.SubmittedByUserId != coachUserId || attendance.Status != "Verified"))
                throw new SwimClassStateConflictException("Cần chụp ảnh trước khi xác nhận có mặt.");
            previousImageUrl = attendance?.ImageUrl;
            if (attendance is null)
            {
                attendance = new ClassAttendance
                {
                    Id = Guid.NewGuid(), SessionId = request.SessionId, RegistrationId = request.RegistrationId,
                    SubmittedByUserId = coachUserId, SubmittedAt = now, CreatedAt = now, UpdatedAt = now
                };
                context.ClassAttendances.Add(attendance);
            }
            else
                attendance.UpdatedAt = NextVersionTimestamp(now, attendance.UpdatedAt);

            attendance.Status = request.Status;
            attendance.ImageUrl = request.Status == "Absent" ? null : uploadedUrl ?? attendance.ImageUrl;
            attendance.SubmittedByUserId = coachUserId;
            attendance.SubmittedAt = now;
            if (attendance.ImageUrl?.Length > 2048)
                throw new InvalidOperationException("Đường dẫn ảnh điểm danh vượt quá giới hạn lưu trữ.");
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            if (previousImageUrl is not null && previousImageUrl != attendance.ImageUrl)
                await DeletePreviousAttendanceImageAsync(previousImageUrl);
            return ToAttendanceResponse(attendance, request.ClassId);
        }
        finally
        {
            if (!committed && uploadedPublicId is not null)
            {
                try { await cloudinary.DestroyAsync(new DeletionParams(uploadedPublicId)); }
                catch (Exception exception) { logger.LogWarning(exception, "Không xóa được ảnh điểm danh {PublicId}", uploadedPublicId); }
            }
        }
    }

    private async Task DeletePreviousAttendanceImageAsync(string imageUrl)
    {
        var match = Regex.Match(imageUrl, @"smartpool/class-attendances/[0-9a-f]{32}(?=\.[a-z]+(?:\?|$))",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success) return;
        try
        {
            var deleted = await cloudinary.DestroyAsync(new DeletionParams(match.Value));
            if (deleted.Error is not null)
                logger.LogWarning("Không xóa được ảnh điểm danh {PublicId}: {Error}", match.Value, deleted.Error.Message);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Không xóa được ảnh điểm danh {PublicId}", match.Value);
        }
    }
}
