using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartPool.Infrastructure.Services;

namespace SmartPool.API.Hubs
{
    /// <summary>
    /// SignalR Hub xử lý kết nối real-time từ client.
    /// Kế thừa NotificationHubBase (Infrastructure) để IHubContext&lt;NotificationHubBase&gt; inject được.
    /// </summary>
    [Authorize] // Yêu cầu JWT — bỏ comment khi đã cấu hình Auth
    public class NotificationHub : NotificationHubBase
    {
        public override async Task OnConnectedAsync()
        {
            // Tự động join user vào group theo userId
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Cho phép gửi thông báo real-time trực tiếp qua Hub đến tất cả clients.
        /// </summary>
        public async Task SendNotification(string title, string content, string type)
        {
            var payload = new
            {
                id = Guid.NewGuid(),
                title = string.IsNullOrWhiteSpace(title) ? "Thông báo hệ thống" : title,
                content = string.IsNullOrWhiteSpace(content) ? "Nội dung thông báo mới" : content,
                type = string.IsNullOrWhiteSpace(type) ? "SYSTEM" : type,
                isRead = false,
                createdAt = DateTime.UtcNow
            };

            await Clients.All.SendAsync("ReceiveNotification", payload);
        }
    }
}