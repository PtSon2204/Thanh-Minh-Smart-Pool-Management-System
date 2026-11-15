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
        var all = await db.Notifications.AsNoTracking().ToListAsync(cancellationToken);

        var filtered = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Type))
            filtered = filtered.Where(n => n.Type == request.Type);

        if (request.IsRead.HasValue)
            filtered = filtered.Where(n => n.IsRead == request.IsRead.Value);

        var ordered = filtered.OrderByDescending(n => n.CreatedAt).ToList();

        var totalCount = ordered.Count;
        var unreadCount = all.Count(n => n.IsRead != true);

        var items = ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new GetNotificationResponse
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Content = n.Content,
                Type = n.Type,
                IsRead = n.IsRead ?? false,
                CreatedAt = n.CreatedAt,
            })
            .ToList();

        return new GetNotificationsResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            UnreadCount = unreadCount,
        };
    }
}
