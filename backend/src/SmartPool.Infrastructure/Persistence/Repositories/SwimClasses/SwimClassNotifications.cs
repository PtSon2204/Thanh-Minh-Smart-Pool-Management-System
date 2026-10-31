using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    private async Task<List<Notification>> AddClassNotificationsAsync(
        Guid classId,
        IEnumerable<Guid> coachUserIds,
        string title,
        string content,
        string type,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var recipients = await context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == classId && item.Status == "Confirmed")
            .Select(item => item.CustomerUserId)
            .ToListAsync(cancellationToken);
        recipients.AddRange(coachUserIds);

        var notifications = recipients.Distinct()
            .Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Content = content,
                Type = type,
                IsRead = false,
                CreatedAt = now
            })
            .ToList();
        context.Notifications.AddRange(notifications);
        return notifications;
    }

    private async Task PublishClassNotificationsAsync(
        Guid classId,
        IReadOnlyList<Notification> notifications)
    {
        foreach (var notification in notifications)
        {
            try
            {
                await notificationService.SendToUserAsync(
                    notification.UserId.ToString(),
                    "notification_received",
                    new
                    {
                        notification.Id,
                        ClassId = classId,
                        notification.Title,
                        notification.Content,
                        notification.Type,
                        notification.CreatedAt
                    },
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                // The database record is already committed and remains available.
                logger.LogWarning(exception,
                    "Could not deliver class notification {NotificationId} to user {UserId}.",
                    notification.Id, notification.UserId);
            }
        }
    }
}
