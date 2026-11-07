using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelMyClassRegistration;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<CancelMyClassRegistrationResponse> CancelMyRegistrationAsync(
        Guid classId, Guid customerUserId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(classId, cancellationToken);

        var registration = await context.ClassRegistrations.SingleOrDefaultAsync(
            item => item.ClassId == classId && item.CustomerUserId == customerUserId,
            cancellationToken) ?? throw new KeyNotFoundException("Bạn chưa đăng ký lớp này.");
        if (registration.Status != "Confirmed")
            throw new SwimClassStateConflictException("Đăng ký lớp không còn ở trạng thái Confirmed.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var firstScheduledStart = await context.ClassSessions.AsNoTracking()
            .Where(item => item.ClassId == classId && item.Status == "Scheduled")
            .OrderBy(item => item.StartsAt)
            .Select(item => (DateTime?)item.StartsAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (!ClassRegistrationCancellationPolicy.CanSelfCancel(firstScheduledStart, now))
            throw new SwimClassStateConflictException(
                "Đã đến giờ bắt đầu buổi học đầu tiên; vui lòng liên hệ Coach để hủy đăng ký.");

        registration.Status = "Cancelled";
        registration.CancelledAt = now;
        registration.CancelledByUserId = customerUserId;
        registration.CancelReason = null;
        registration.UpdatedAt = NextVersionTimestamp(now, registration.UpdatedAt);
        var coachUserId = await context.SwimClasses.AsNoTracking()
            .Where(item => item.Id == classId)
            .Select(item => item.CoachUserId)
            .SingleAsync(cancellationToken);
        var notifications = new[] { customerUserId, coachUserId }.Distinct()
            .Select(userId => new Notification
            {
                Id = Guid.NewGuid(), UserId = userId,
                Title = "Đăng ký lớp đã được hủy",
                Content = "Một đăng ký lớp dạy bơi đã được Customer hủy.",
                Type = "swim_class_registration_cancelled",
                IsRead = false, CreatedAt = now
            }).ToList();
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishClassNotificationsAsync(classId, notifications);
        return new CancelMyClassRegistrationResponse(
            registration.Id, classId, registration.Status, customerUserId, now);
    }
}
