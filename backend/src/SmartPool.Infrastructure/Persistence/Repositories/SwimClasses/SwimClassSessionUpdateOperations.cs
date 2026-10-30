using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<UpdateClassSessionResponse> UpdateSessionAsync(
        UpdateClassSessionCommand request,
        Guid coachUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(request.ClassId, cancellationToken);
        await LockPoolScheduleAsync(cancellationToken);

        var session = await context.ClassSessions
            .Include(item => item.Class)
            .SingleOrDefaultAsync(item => item.Id == request.SessionId &&
                item.ClassId == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy buổi học của lớp.");

        EnsureClassOwner(session.Class, coachUserId);

        if (session.UpdatedAt != request.ExpectedUpdatedAt.UtcDateTime)
            throw new SwimClassStateConflictException(
                "Buổi học đã được người khác cập nhật. Vui lòng tải lại trước khi lưu.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (session.Class.Status is not ("Draft" or "Active") ||
            session.Status != "Scheduled" || session.StartsAt <= now)
            throw new SwimClassStateConflictException(
                "Chỉ có thể sửa buổi đã lên lịch và chưa diễn ra của lớp đang hoạt động hoặc bản nháp.");

        var startsAt = request.StartsAt.UtcDateTime;
        var endsAt = request.EndsAt.UtcDateTime;
        if (startsAt <= now)
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.StartsAt), "Buổi học phải bắt đầu trong tương lai.")
            });

        if (session.Class.Status == "Active" && startsAt < session.Class.RegistrationEndAt)
            throw new SwimClassStateConflictException(
                "Buổi học của lớp Active không được bắt đầu trước hạn kết thúc đăng ký.");

        await LockCoachAsync(session.CoachUserId, cancellationToken);

        var existingSessions = await context.ClassSessions.AsNoTracking()
            .Where(item => item.Id != request.SessionId && item.Status != "Cancelled" &&
                item.StartsAt < endsAt && item.EndsAt > startsAt)
            .Select(item => new ExistingClassSession(
                item.Id, item.ClassId, item.CoachUserId, item.StartsAt, item.EndsAt))
            .ToListAsync(cancellationToken);
        var proposed = new ProposedClassSession(
            0, session.CoachUserId, startsAt, endsAt);
        var conflicts = ClassSessionConflictDetector.Find(
            request.ClassId, [proposed], existingSessions);
        if (conflicts.Count != 0)
            throw new ClassSessionConflictException(
                "Buổi học bị trùng lịch lớp hoặc lịch Coach.", conflicts);

        var changed = session.StartsAt != startsAt || session.EndsAt != endsAt;
        session.StartsAt = startsAt;
        session.EndsAt = endsAt;
        var updatedAt = NextVersionTimestamp(now, session.UpdatedAt, session.Class.UpdatedAt);
        session.UpdatedAt = updatedAt;
        session.Class.UpdatedAt = updatedAt;

        var notifications = new List<SmartPool.Domain.Entities.Notification>();
        if (changed)
            notifications = await AddClassNotificationsAsync(
                request.ClassId, [session.CoachUserId],
                "Lịch lớp dạy bơi thay đổi",
                $"Lịch học của lớp {session.Class.Name} đã được cập nhật. Vui lòng xem lại thời khóa biểu.",
                "swim_class_session_updated", now, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await PublishClassNotificationsAsync(request.ClassId, notifications);
        return new UpdateClassSessionResponse(
            session.Id, session.ClassId, session.CoachUserId, session.StartsAt,
            session.EndsAt, session.Status, session.CreatedAt, session.UpdatedAt);
    }
}
