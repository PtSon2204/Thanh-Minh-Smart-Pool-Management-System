using System.Text.Json.Serialization;
using MediatR;
using SmartPool.Application.Features.Notifications.Queries.GetNotifications;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.Notifications;

public sealed record GetMyNotificationsQuery(int Page = 1, int PageSize = 20) : IRequest<GetNotificationsResult>
{
    [JsonIgnore] public Guid UserId { get; init; }
}

public sealed record MarkMyNotificationReadCommand(Guid NotificationId) : IRequest<bool>
{
    [JsonIgnore] public Guid UserId { get; init; }
}

public sealed class GetMyNotificationsHandler(IStaffOperations operations)
    : IRequestHandler<GetMyNotificationsQuery, GetNotificationsResult>
{
    public Task<GetNotificationsResult> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken) =>
        operations.GetMyNotificationsAsync(request.UserId, request.Page, request.PageSize, cancellationToken);
}

public sealed class MarkMyNotificationReadHandler(IStaffOperations operations)
    : IRequestHandler<MarkMyNotificationReadCommand, bool>
{
    public Task<bool> Handle(MarkMyNotificationReadCommand request, CancellationToken cancellationToken) =>
        operations.MarkMyNotificationReadAsync(request.UserId, request.NotificationId, cancellationToken);
}
