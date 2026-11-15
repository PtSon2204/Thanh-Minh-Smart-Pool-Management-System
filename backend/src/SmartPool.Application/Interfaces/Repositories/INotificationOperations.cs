using SmartPool.Application.Features.Notifications.Queries.GetNotifications;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface INotificationOperations
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Notification notification, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken);
    Task UpdateAsync(Notification notification, CancellationToken cancellationToken);
    Task<GetNotificationsResult> GetNotificationsAsync(GetNotificationsQuery query, CancellationToken cancellationToken);
}
