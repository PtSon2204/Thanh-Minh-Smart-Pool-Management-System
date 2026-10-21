using MediatR;

namespace SmartPool.Application.Features.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsResult
{
    public IReadOnlyList<GetNotificationResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int UnreadCount { get; set; }
}
