using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Caching;
using SmartPool.Infrastructure.Persistence;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;
using SmartPool.Infrastructure.Services;
using SmartPool.Infrastructure.Storages;
using System.Security.Claims;
using System.Text;


namespace SmartPool.Infrastructure
{
    /// <summary>
    /// Extension method đăng ký toàn bộ services của tầng Infrastructure.
    /// Mỗi thành viên thêm service của mình vào đây, KHÔNG sửa Program.cs.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database — Entity Framework Core + PostgreSQL (Npgsql)
            services.AddDbContext<SmartPoolDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(SmartPoolDbContext).Assembly.FullName)));

            // JWT Authentication
            var jwtSecret = configuration["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                        ValidateIssuer           = true,
                        ValidIssuer              = configuration["JwtSettings:Issuer"],
                        ValidateAudience         = true,
                        ValidAudience            = configuration["JwtSettings:Audience"],
                        ValidateLifetime         = true,
                        ClockSkew                = TimeSpan.Zero, // Không cho phép trễ giờ
                    };

                    // Cho phép SignalR gửi token qua query string
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                                context.Token = accessToken;
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = async context =>
                        {
                            var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                            var tokenRole = context.Principal?.FindFirstValue(ClaimTypes.Role);
                            if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty ||
                                string.IsNullOrWhiteSpace(tokenRole))
                            {
                                context.Fail("Token identity is invalid.");
                                return;
                            }

                            var validator = context.HttpContext.RequestServices
                                .GetRequiredService<IAccessTokenService>();
                            if (!await validator.IsCurrentAsync(userId, tokenRole,
                                context.HttpContext.RequestAborted))
                                context.Fail("Account role or status changed.");
                        }
                    };
                });

            // Redis Distributed Cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName  = "SmartPool:";
            });

            // Cloudinary — lưu trữ ảnh
            var cloudinaryUrl = $"cloudinary://" +
                $"{configuration["CloudinarySettings:ApiKey"]}:" +
                $"{configuration["CloudinarySettings:ApiSecret"]}@" +
                $"{configuration["CloudinarySettings:CloudName"]}";

            services.AddSingleton(new Cloudinary(cloudinaryUrl) { Api = { Secure = true } });
            services.AddScoped<IStorageService, CloudinaryStorageService>();

            // Email — MailKit
            services.AddScoped<IEmailService, EmailService>();
            services.AddSingleton<IEmailHistoryStore, EmailHistoryStore>();

            // Password hashing — ASP.NET Core Identity (salt và hash do PasswordHasher quản lý)
            services.AddScoped<IPasswordHasher, PasswordHasherService>();

            // Access token generation for login
            services.AddScoped<IAccessTokenService, AccessTokenService>();

            // Identity of the authenticated caller in the current HTTP request
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();

            // SignalR — real-time notifications
            services.AddSignalR();
            services.AddScoped<INotificationService, SignalRNotificationService>();

            // Cache Service wrapper
            services.AddScoped<ICacheService, RedisCacheService>();

            // Repository & Unit of Work
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUserOperations, UserOperations>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IServiceOperations, ServiceOperations>();
            services.AddScoped<IStaffOperations, StaffOperations>();
            services.AddScoped<IPoolAccessOperations, PoolAccessOperations>();
            services.AddScoped<IVoucherOperations, VoucherOperations>();
            services.AddScoped<ITicketTypeOperations, SmartPool.Infrastructure.Persistence.Repositories.Tickets.TicketTypeOperations>();

            return services;
        }
    }
}
