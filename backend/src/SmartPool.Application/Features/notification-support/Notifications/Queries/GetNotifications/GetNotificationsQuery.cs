using MediatR;

namespace SmartPool.Application.Features.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQuery : IRequest<GetNotificationsResult>
{
    /// <summary>Lọc theo loại: ticket_day, ticket_month, rental, incident, system</summary>
    public string? Type { get; set; }

    /// <summary>Lọc chưa đọc (false) / đã đọc (true) / null = tất cả</summary>
    public bool? IsRead { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class GetNotificationsResult
{
    public IReadOnlyList<GetNotificationResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int UnreadCount { get; set; }
}
