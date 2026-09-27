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
    }
}
