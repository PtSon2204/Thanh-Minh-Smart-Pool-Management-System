
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Services
{
    [Authorize]
    public class NotificationHubBase : Hub
    {
        private readonly ILogger<NotificationHubBase>? _logger;

        public NotificationHubBase(ILogger<NotificationHubBase>? logger = null)
            => _logger = logger;

        // Kiem tra role khong phan biet chu hoa/thuong
        private bool HasRole(string role)
        {
            var user = Context.User;

            if (user?.Identity?.IsAuthenticated != true)
                return false;

            var roleClaimType = (user.Identity as ClaimsIdentity)?.RoleClaimType
                ?? ClaimTypes.Role;

            return user.Claims.Any(c =>
                c.Type == roleClaimType &&
                string.Equals(c.Value, role, StringComparison.OrdinalIgnoreCase));
        }

        // ADMIN va LIFEGUARD deu duoc xem phan tich camera
        [Authorize]
        public Task SubscribeCameraAnalysis(Guid cameraId)
        {
            if (cameraId == Guid.Empty ||
                (!HasRole("ADMIN") && !HasRole("LIFEGUARD")))
            {
                LogPrincipalDecision("Subscribe-denied", cameraId);
                throw new HubException("Không có quyền xem phân tích camera.");
            }

            LogPrincipalDecision("Subscribe-ok", cameraId);

            return Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"camera-analysis_{cameraId:N}"
            );
        }

        public Task UnsubscribeCameraAnalysis(Guid cameraId) =>
            Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"camera-analysis_{cameraId:N}"
            );

        public override async Task OnConnectedAsync()
        {
            LogPrincipalDecision("Connect", null);

            var userId = Context.UserIdentifier;

            if (!string.IsNullOrEmpty(userId))
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"user_{userId}"
                );

            // ADMIN va LIFEGUARD nhan canh bao AI
            if (HasRole("ADMIN") || HasRole("LIFEGUARD"))
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    "safety-operators"
                );

            await base.OnConnectedAsync();
        }

        private void LogPrincipalDecision(string eventName, Guid? cameraId)
        {
            var principal = Context.User;
            var identity = principal?.Identity;

            var roles = principal?.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToArray() ?? [];

            _logger?.LogWarning(
                "SignalR principal {Event}: ConnectionId={ConnectionId}, UserId={UserId}, Authenticated={Authenticated}, AuthenticationType={AuthenticationType}, RoleClaimType={RoleClaimType}, RoleClaims={RoleClaims}, IsInRoleAdmin={IsInRoleAdmin}, IsInRoleLifeguard={IsInRoleLifeguard}, CameraId={CameraId}",
                eventName,
                Context.ConnectionId,
                Context.UserIdentifier,
                identity?.IsAuthenticated,
                identity?.AuthenticationType,
                identity is ClaimsIdentity claimsIdentity
                    ? claimsIdentity.RoleClaimType
                    : null,
                roles,
                HasRole("ADMIN"),
                HasRole("LIFEGUARD"),
                cameraId
            );
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;

            if (!string.IsNullOrEmpty(userId))
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    $"user_{userId}"
                );

            await base.OnDisconnectedAsync(exception);
        }
    }
}
