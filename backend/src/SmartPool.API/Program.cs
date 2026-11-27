using DotNetEnv;
using Microsoft.AspNetCore.HttpOverrides;
using SmartPool.API.Extensions;
using SmartPool.Application;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Seeding;

namespace SmartPool.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Load .env (gitignored — secrets của từng máy)
            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            if (!File.Exists(envFilePath))
                envFilePath = Path.Combine(AppContext.BaseDirectory, ".env");
            if (File.Exists(envFilePath))
                Env.NoClobber().Load(envFilePath);

            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddEnvironmentVariables();

            // Tin cậy X-Forwarded-For / X-Forwarded-Proto từ reverse proxy (Render, Nginx...)
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // CORS — đọc origin từ .env
            var allowedOrigins = (builder.Configuration["CorsSettings:AllowedOrigins"] ?? "http://localhost:5173")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            builder.Services.AddCors(options =>
                options.AddPolicy("SmartPoolCorsPolicy", policy => policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("Retry-After")
                    .AllowCredentials()));

            builder.Services.AddApplicationServices(builder.Configuration);     // Application/DependencyInjection.cs
            builder.Services.AddInfrastructureServices(builder.Configuration);  // Infrastructure/DependencyInjection.cs
            builder.Services.AddApiAuthorization();

            // API layer services
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Build & Middleware pipeline
            var app = builder.Build();

            app.UseForwardedHeaders();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseHttpsRedirection();
            }

            app.UseCors("SmartPoolCorsPolicy");     // Phải đứng TRƯỚC Auth
            app.UseAuthentication();
            app.Use(async (context, next) =>
            {
                var endpoint = context.GetEndpoint();
                var protectedRoute = endpoint?.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Count > 0
                    && endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is null;
                var path = context.Request.Path.Value?.TrimEnd('/');
                var allowed = (HttpMethods.IsPut(context.Request.Method) && string.Equals(path, "/api/auth/change-password", StringComparison.OrdinalIgnoreCase))
                    || (HttpMethods.IsPost(context.Request.Method) && string.Equals(path, "/api/auth/logout", StringComparison.OrdinalIgnoreCase));
                if (context.User.Identity?.IsAuthenticated == true &&
                    context.User.FindFirst("purpose")?.Value == "password_change" && protectedRoute && !allowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { code = "FORCED_PASSWORD_CHANGE" });
                    return;
                }
                await next(context);
            });
            app.UseAuthorization();
            app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
            app.MapControllers();
            app.MapHubs();                          // SignalR Hubs — xem API/Extensions/HubExtensions.cs

            // Tự động sync role permissions, admin khởi tạo (nếu cấu hình) và ca làm việc vào DB khi app khởi động.
            // Thêm tính năng mới? Sửa RolePermissionSeeder.cs là đủ.
            await RolePermissionSeeder.SeedAsync(app.Services);
            await AdminUserSeeder.SeedAsync(app.Services);
            await ShiftSeeder.SeedAsync(app.Services);

            app.Run();
        }
    }
}
