using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.DeactivateSwimClass;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<DeactivateSwimClassResponse> DeactivateAsync(
        DeactivateSwimClassCommand request, Guid coachUserId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(request.ClassId, cancellationToken);
        var swimClass = await context.SwimClasses.SingleOrDefaultAsync(
            item => item.Id == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");
        EnsureClassOwner(swimClass, coachUserId);
        if (swimClass.UpdatedAt != request.ExpectedUpdatedAt.UtcDateTime)
            throw new SwimClassStateConflictException(
                "Lớp đã được cập nhật. Vui lòng tải lại trước khi ngừng lớp.");
        if (swimClass.Status is not ("Draft" or "Active"))
            throw new SwimClassStateConflictException("Chỉ có thể ngừng lớp Draft hoặc Active.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reason = request.Reason.Trim();
        var futureSessions = await context.ClassSessions
            .Where(item => item.ClassId == request.ClassId && item.Status == "Scheduled" &&
                item.StartsAt > now)
            .ToListAsync(cancellationToken);
        // Resolve recipients while the class and its sessions still have their original state.
        var notifications = await AddClassNotificationsAsync(request.ClassId,
            futureSessions.Select(item => item.CoachUserId).Append(coachUserId),
            "Lớp dạy bơi đã ngừng hoạt động",
            $"Lớp {swimClass.Name} đã ngừng hoạt động. Lý do: {reason}",
            "swim_class_deactivated", now, cancellationToken);
        foreach (var session in futureSessions)
        {
            session.Status = "Cancelled";
            session.CancelReason = reason;
            session.UpdatedAt = NextVersionTimestamp(now, session.UpdatedAt);
        }

        swimClass.Status = "Inactive";
        swimClass.InactiveReason = reason;
        swimClass.InactivatedAt = now;
        swimClass.UpdatedAt = NextVersionTimestamp(now, swimClass.UpdatedAt);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishClassNotificationsAsync(request.ClassId, notifications);
        return new DeactivateSwimClassResponse(swimClass.Id, swimClass.Status,
            reason, now, swimClass.UpdatedAt);
    }
}
