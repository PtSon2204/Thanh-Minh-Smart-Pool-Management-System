using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Services
{
    [Authorize]
    public class NotificationHubBase : Hub
    {
        [Authorize(Roles = "ADMIN")]
        public Task SubscribeCameraAnalysis(Guid cameraId)
        {
            if (cameraId == Guid.Empty || Context.User?.IsInRole("ADMIN") != true)
                throw new HubException("Không có quyền xem phân tích camera.");
            return Groups.AddToGroupAsync(Context.ConnectionId, $"camera-analysis_{cameraId:N}");
        }

        public Task UnsubscribeCameraAnalysis(Guid cameraId) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, $"camera-analysis_{cameraId:N}");

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            // Nhóm vận hành an toàn: chỉ ADMIN và LIFEGUARD nhận sự kiện cảnh báo AI.
            if (Context.User?.IsInRole("ADMIN") == true || Context.User?.IsInRole("LIFEGUARD") == true)
                await Groups.AddToGroupAsync(Context.ConnectionId, "safety-operators");

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
