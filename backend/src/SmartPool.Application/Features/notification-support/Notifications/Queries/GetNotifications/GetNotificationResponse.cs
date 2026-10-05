namespace SmartPool.Application.Features.Notifications.Queries.GetNotifications;

public sealed class GetNotificationResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? CreatedAt { get; set; }
}
