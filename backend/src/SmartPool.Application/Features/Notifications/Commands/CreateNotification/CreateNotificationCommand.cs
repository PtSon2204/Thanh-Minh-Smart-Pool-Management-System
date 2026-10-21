using MediatR;

namespace SmartPool.Application.Features.Notifications.Commands.CreateNotification;

public sealed record CreateNotificationCommand : IRequest<Guid>
{
    public Guid UserId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    /// <summary>Loại thông báo: ticket_day, ticket_month, rental, incident, system, ...</summary>
    public string Type { get; init; } = "system";
}
