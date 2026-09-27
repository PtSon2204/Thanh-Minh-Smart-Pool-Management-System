using Microsoft.AspNetCore.SignalR;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Services
{
    public abstract class NotificationHubBase : Hub { }

    /// <summary>
    /// Gửi thông báo real-time đến clients qua SignalR.
    /// </summary>
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHubBase> _hubContext;

        public SignalRNotificationService(IHubContext<NotificationHubBase> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendToUserAsync(string userId, string eventName, object payload, CancellationToken cancellationToken = default)
            => await _hubContext.Clients.User(userId).SendAsync(eventName, payload, cancellationToken);

        public async Task SendToAllAsync(string eventName, object payload, CancellationToken cancellationToken = default)
            => await _hubContext.Clients.All.SendAsync(eventName, payload, cancellationToken);

        public async Task SendToGroupAsync(string groupName, string eventName, object payload, CancellationToken cancellationToken = default)
            => await _hubContext.Clients.Group(groupName).SendAsync(eventName, payload, cancellationToken);
    }
}
