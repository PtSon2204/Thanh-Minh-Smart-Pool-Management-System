using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassAttendances;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<ClassAttendanceResponse> SubmitAttendanceAsync(Guid classId, Guid sessionId,
        Guid customerUserId, byte[] image, string format, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(image, writable: false);
        var uploaded = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription($"attendance.{format}", stream),
            Folder = "smartpool/class-attendances",
            PublicId = Guid.NewGuid().ToString("N"),
            Overwrite = false
        });
        if (uploaded.Error is not null || uploaded.SecureUrl is null ||
            string.IsNullOrWhiteSpace(uploaded.PublicId))
            throw new InvalidOperationException(uploaded.Error?.Message ?? "Không thể lưu ảnh điểm danh.");

        var committed = false;
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await LockClassAsync(classId, cancellationToken);
            var submission = await GetAttendanceSubmissionContextAsync(
                classId, sessionId, customerUserId, cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            ClassAttendanceSubmissionPolicy.EnsureAllowed(submission, now);
            var attendance = new ClassAttendance
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                RegistrationId = submission.RegistrationId,
                ImageUrl = uploaded.SecureUrl.AbsoluteUri,
                Status = "PendingReview",
                SubmittedByUserId = customerUserId,
                SubmittedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            if (attendance.ImageUrl.Length > 2048)
                throw new InvalidOperationException("Đường dẫn ảnh điểm danh vượt quá giới hạn lưu trữ.");

            context.ClassAttendances.Add(attendance);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return ToAttendanceResponse(attendance, classId);
        }
        finally
        {
            if (!committed)
            {
                try
                {
                    var deleted = await cloudinary.DestroyAsync(new DeletionParams(uploaded.PublicId));
                    if (deleted.Error is not null)
                        logger.LogWarning("Không xóa được ảnh điểm danh {PublicId}: {Error}",
                            uploaded.PublicId, deleted.Error.Message);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Không xóa được ảnh điểm danh {PublicId}",
                        uploaded.PublicId);
                }
            }
        }
    }

    public async Task<PagedResponse<MyClassAttendanceItem>> GetMyAttendancesAsync(Guid classId,
        Guid customerUserId, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var registration = await context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == classId && item.CustomerUserId == customerUserId)
            .Select(item => new { item.Id, item.Status })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Bạn chưa đăng ký lớp này.");

        var sessions = context.ClassSessions.AsNoTracking()
            .Where(item => item.ClassId == classId);
        var totalCount = await sessions.CountAsync(cancellationToken);
        var page = await sessions
            .OrderByDescending(item => item.StartsAt)
            .ThenByDescending(item => item.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new { item.Id, item.StartsAt, item.EndsAt, item.Status })
            .ToListAsync(cancellationToken);
        var sessionIds = page.Select(item => item.Id).ToArray();
        var attendanceBySession = await context.ClassAttendances.AsNoTracking()
            .Where(item => item.RegistrationId == registration.Id &&
                sessionIds.Contains(item.SessionId))
            .ToDictionaryAsync(item => item.SessionId, cancellationToken);

        return new PagedResponse<MyClassAttendanceItem>
        {
            Items = page.Select(session => new MyClassAttendanceItem(
                session.Id, session.StartsAt, session.EndsAt, session.Status,
                registration.Status,
                attendanceBySession.TryGetValue(session.Id, out var attendance)
                    ? ToAttendanceResponse(attendance, classId)
                    : null)).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<AttendanceSubmissionContext> GetAttendanceSubmissionContextAsync(Guid classId,
        Guid sessionId, Guid customerUserId, CancellationToken cancellationToken)
    {
        var session = await context.ClassSessions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.ClassId == classId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy buổi học của lớp.");
        var registration = await context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == classId && item.CustomerUserId == customerUserId)
            .Select(item => new { item.Id, item.Status })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Bạn chưa đăng ký lớp này.");
        var alreadySubmitted = await context.ClassAttendances.AsNoTracking().AnyAsync(item =>
            item.SessionId == sessionId && item.RegistrationId == registration.Id,
            cancellationToken);
        return new AttendanceSubmissionContext(registration.Id, registration.Status,
            session.Status, session.StartsAt, session.EndsAt, alreadySubmitted);
    }

    private static ClassAttendanceResponse ToAttendanceResponse(ClassAttendance item, Guid classId) =>
        new(item.Id, classId, item.SessionId, item.RegistrationId,
            item.ImageUrl, item.Status, item.SubmittedAt, item.ReviewedAt,
            item.ReviewNote, item.UpdatedAt);
}
