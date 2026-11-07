using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.Notifications.Queries.GetNotifications;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    public async Task<GetNotificationsResult> GetMyNotificationsAsync(Guid userId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = context.Notifications.AsNoTracking().Where(item => item.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var unread = await query.CountAsync(item => item.IsRead != true, cancellationToken);
        var items = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new GetNotificationResponse { Id = item.Id, UserId = item.UserId, Title = item.Title,
                Content = item.Content, Type = item.Type, IsRead = item.IsRead == true, CreatedAt = item.CreatedAt })
            .ToListAsync(cancellationToken);
        return new GetNotificationsResult { Items = items, TotalCount = total, Page = page, PageSize = pageSize, UnreadCount = unread };
    }

    public async Task<bool> MarkMyNotificationReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var changed = await context.Notifications.Where(item => item.Id == notificationId && item.UserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsRead, true), cancellationToken);
        return changed != 0;
    }
}
