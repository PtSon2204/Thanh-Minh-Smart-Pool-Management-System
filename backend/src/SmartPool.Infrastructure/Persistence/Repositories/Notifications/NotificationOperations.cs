using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.Notifications.Queries.GetNotifications;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Notifications;

public sealed class NotificationOperations(SmartPoolDbContext db) : INotificationOperations
{
    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        await db.Notifications.AddAsync(notification, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken)
    {
        await db.Notifications.AddRangeAsync(notifications, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Notification notification, CancellationToken cancellationToken)
    {
        db.Notifications.Update(notification);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<GetNotificationsResult> GetNotificationsAsync(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = db.Notifications.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = request.Type.Trim();
            query = query.Where(n => n.Type == type);
        }

        if (request.IsRead.HasValue)
        {
            query = request.IsRead.Value
                ? query.Where(n => n.IsRead == true)
                : query.Where(n => n.IsRead != true);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await db.Notifications.AsNoTracking().CountAsync(n => n.IsRead != true, cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new GetNotificationResponse
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Content = n.Content,
                Type = n.Type,
                IsRead = n.IsRead == true,
                CreatedAt = n.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new GetNotificationsResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            UnreadCount = unreadCount,
        };
    }
}
