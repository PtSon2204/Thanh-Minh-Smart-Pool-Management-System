using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassSession;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<CancelClassSessionResponse> CancelSessionAsync(
        CancelClassSessionCommand request, Guid coachUserId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(request.ClassId, cancellationToken);

        var session = await context.ClassSessions.Include(item => item.Class)
            .SingleOrDefaultAsync(item => item.Id == request.SessionId &&
                item.ClassId == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy buổi học của lớp.");
        EnsureClassOwner(session.Class, coachUserId);

        if (session.UpdatedAt != request.ExpectedUpdatedAt.UtcDateTime)
            throw new SwimClassStateConflictException(
                "Buổi học đã được cập nhật. Vui lòng tải lại trước khi hủy.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (session.Class.Status is not ("Draft" or "Active") ||
            session.Status != "Scheduled" || session.StartsAt <= now)
            throw new SwimClassStateConflictException(
                "Chỉ có thể hủy buổi Scheduled chưa bắt đầu của lớp Draft hoặc Active.");

        session.Status = "Cancelled";
        session.CancelReason = request.Reason.Trim();
        var updatedAt = NextVersionTimestamp(now, session.UpdatedAt, session.Class.UpdatedAt);
        session.UpdatedAt = updatedAt;
        session.Class.UpdatedAt = updatedAt;

        var notifications = session.Class.Status == "Active"
            ? await AddClassNotificationsAsync(request.ClassId, [session.CoachUserId],
                "Buổi học đã bị hủy",
                $"Buổi học của lớp {session.Class.Name} đã bị hủy. Lý do: {session.CancelReason}",
                "swim_class_session_cancelled", now, cancellationToken)
            : [];

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishClassNotificationsAsync(request.ClassId, notifications);
        return new CancelClassSessionResponse(
            session.Id, session.ClassId, session.Status, session.CancelReason, session.UpdatedAt);
    }
}
