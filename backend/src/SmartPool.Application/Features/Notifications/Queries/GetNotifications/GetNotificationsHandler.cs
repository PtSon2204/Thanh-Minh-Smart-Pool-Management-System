using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsHandler : IRequestHandler<GetNotificationsQuery, GetNotificationsResult>
{
    private readonly INotificationOperations _notificationOperations;

    public GetNotificationsHandler(INotificationOperations notificationOperations)
    {
        _notificationOperations = notificationOperations;
    }

    public async Task<GetNotificationsResult> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        return await _notificationOperations.GetNotificationsAsync(request, cancellationToken);
    }
}
