using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassSessionAttendances;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<PagedResponse<ClassSessionAttendanceItem>> GetClassSessionAttendancesAsync(
        GetClassSessionAttendancesQuery request, Guid coachUserId,
        CancellationToken cancellationToken)
    {
        var assignedCoachId = await context.ClassSessions.AsNoTracking()
            .Where(item => item.Id == request.SessionId && item.ClassId == request.ClassId)
            .Select(item => (Guid?)item.CoachUserId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy buổi học của lớp.");
        if (assignedCoachId != coachUserId)
            throw new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được xem điểm danh.");

        var query = context.ClassAttendances.AsNoTracking()
            .Where(item => item.SessionId == request.SessionId &&
                item.Registration.ClassId == request.ClassId);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(item => EF.Functions.ILike(item.Status, status));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.SubmittedAt)
            .ThenByDescending(item => item.Id)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new ClassSessionAttendanceItem(
                item.Id,
                item.RegistrationId,
                item.Registration.CustomerUserId,
                item.Registration.CustomerUser.UserProfile != null
                    ? item.Registration.CustomerUser.UserProfile.FullName
                    : item.Registration.CustomerUser.Username,
                item.ImageUrl,
                item.Status,
                item.SubmittedAt,
                item.ReviewedByUserId,
                item.ReviewedAt,
                item.ReviewNote,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ClassSessionAttendanceItem>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ReviewClassAttendanceResponse> ReviewClassAttendanceAsync(
        ReviewClassAttendanceCommand request, Guid coachUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(request.ClassId, cancellationToken);

        var session = await context.ClassSessions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.SessionId &&
                item.ClassId == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy buổi học của lớp.");
        ClassAttendanceReviewPolicy.EnsureAssignedCoach(session.CoachUserId, coachUserId);
        var attendance = await context.ClassAttendances.Include(item => item.Registration)
            .SingleOrDefaultAsync(item => item.Id == request.AttendanceId &&
                item.SessionId == request.SessionId &&
                item.Registration.ClassId == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy điểm danh của buổi học.");

        ClassAttendanceReviewPolicy.EnsureReviewable(session.Status, attendance.Status, attendance.UpdatedAt,
            request.ExpectedUpdatedAt);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var decision = string.Equals(request.Decision.Trim(), "Verified",
            StringComparison.OrdinalIgnoreCase) ? "Verified" : "Rejected";
        var note = string.IsNullOrWhiteSpace(request.ReviewNote)
            ? null : request.ReviewNote.Trim();
        attendance.Status = decision;
        attendance.ReviewNote = note;
        attendance.ReviewedByUserId = coachUserId;
        attendance.ReviewedAt = now;
        attendance.UpdatedAt = NextVersionTimestamp(now, attendance.UpdatedAt);

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = attendance.Registration.CustomerUserId,
            Title = decision == "Verified" ? "Điểm danh đã được xác nhận" : "Điểm danh bị từ chối",
            Content = decision == "Verified"
                ? "Coach đã xác nhận ảnh điểm danh của bạn."
                : $"Coach đã từ chối ảnh điểm danh của bạn. Lý do: {note}",
            Type = "swim_class_attendance_reviewed",
            IsRead = false,
            CreatedAt = now
        };
        context.Notifications.Add(notification);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishClassNotificationsAsync(request.ClassId, [notification]);
        return new ReviewClassAttendanceResponse(attendance.Id, request.ClassId,
            attendance.SessionId, decision, coachUserId, now, note, attendance.UpdatedAt);
    }
}
