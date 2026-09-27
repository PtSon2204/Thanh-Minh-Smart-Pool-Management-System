namespace SmartPool.Application.Interfaces.Services
{
    public interface INotificationService
    {
        /// <summary>Gửi thông báo đến 1 user cụ thể theo userId.</summary>
        Task SendToUserAsync(string userId, string eventName, object payload, CancellationToken cancellationToken = default);

        /// <summary>Gửi thông báo đến tất cả client đang kết nối.</summary>
        Task SendToAllAsync(string eventName, object payload, CancellationToken cancellationToken = default);

        /// <summary>Gửi thông báo đến 1 nhóm (group) SignalR.</summary>
        Task SendToGroupAsync(string groupName, string eventName, object payload, CancellationToken cancellationToken = default);
    }
}
