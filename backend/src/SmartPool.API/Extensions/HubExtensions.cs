using SmartPool.API.Hubs;

namespace SmartPool.API.Extensions
{
    /// <summary>
    /// Đăng ký tất cả SignalR Hub endpoints.
    /// Khi thêm Hub mới: tạo file Hub trong thư mục Hubs/ rồi thêm MapHub ở đây.
    /// KHÔNG sửa Program.cs.
    /// </summary>
    public static class HubExtensions
    {
        public static WebApplication MapHubs(this WebApplication app)
        {
            app.MapHub<NotificationHub>("/hubs/notifications");

            // Thêm Hub mới tại đây:
            // app.MapHub<PoolStatusHub>("/hubs/pool-status");
            // app.MapHub<CameraHub>("/hubs/camera");

            return app;
        }
    }
}
