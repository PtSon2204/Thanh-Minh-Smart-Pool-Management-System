using DotNetEnv;
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
                Env.Load(envFilePath);

            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddEnvironmentVariables();

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

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors("SmartPoolCorsPolicy");     // Phải đứng TRƯỚC Auth
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHubs();                          // SignalR Hubs — xem API/Extensions/HubExtensions.cs

            // Tự động sync role permissions vào DB khi app khởi động.
            // Thêm tính năng mới? Sửa RolePermissionSeeder.cs là đủ.
            await RolePermissionSeeder.SeedAsync(app.Services);

                app.Run();
        }
    }
}
