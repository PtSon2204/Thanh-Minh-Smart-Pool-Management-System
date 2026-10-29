using SmartPool.Infrastructure.Persistence.Repositories.Users;
using SmartPool.Infrastructure.Persistence.Repositories.Services;
using SmartPool.Infrastructure.Persistence.Repositories.Equipments;
using SmartPool.Infrastructure.Persistence.Repositories.Maintenance;
using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;
using SmartPool.Infrastructure.Persistence.Repositories.Tickets;
using SmartPool.Infrastructure.Persistence.Repositories.Vouchers;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.AttendanceQr;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Infrastructure.Caching;
using SmartPool.Infrastructure.Persistence;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;
using SmartPool.Infrastructure.Persistence.Repositories.Roles;
using SmartPool.Infrastructure.Services;
using SmartPool.Infrastructure.Storages;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using SmartPool.Infrastructure.Cameras;
using System.Security.Cryptography.X509Certificates;


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
            services.Configure<CameraOptions>(configuration.GetSection("CameraSettings"));
            var keyRingPath = configuration["CameraSettings:KeyRingPath"] ?? Path.Combine(AppContext.BaseDirectory, "camera-keys");
            var protection = services.AddDataProtection().SetApplicationName("SmartPool.Camera")
                .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
            if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
            else if (!string.IsNullOrWhiteSpace(configuration["CameraSettings:KeyEncryptionCertificatePath"]))
                protection.ProtectKeysWithCertificate(new X509Certificate2(configuration["CameraSettings:KeyEncryptionCertificatePath"]!, configuration["CameraSettings:KeyEncryptionCertificatePassword"]));
            else if (configuration.GetSection("CameraSettings:AllowedEndpoints").GetChildren().Any())
                throw new InvalidOperationException("Camera key encryption requires a certificate on this operating system.");
            services.AddSingleton<CameraAddressPolicy>();
            services.AddSingleton<ICameraFrameDecoder, CameraFrameDecoder>();
            services.AddSingleton<CameraService>();
            services.AddSingleton<ICameraService>(provider => provider.GetRequiredService<CameraService>());
            services.AddHostedService<CameraSupervisor>();
            services.AddSingleton<CameraStreamService>();
            services.AddSingleton<ICameraStreamService>(provider => provider.GetRequiredService<CameraStreamService>());
            services.AddHostedService(provider => provider.GetRequiredService<CameraStreamService>());

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
                            var tokenRoles = context.Principal?.FindAll(ClaimTypes.Role)
                                .Select(claim => claim.Value).ToArray() ?? [];
                            var passwordVersion = context.Principal?.FindFirstValue(AccessTokenService.PasswordVersionClaim);
                            if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty ||
                                tokenRoles.Length == 0 || string.IsNullOrWhiteSpace(passwordVersion))
                            {
                                context.Fail("Token identity is invalid.");
                                return;
                            }

                            var validator = context.HttpContext.RequestServices
                                .GetRequiredService<IAccessTokenService>();
                            if (!await validator.IsCurrentRolesAsync(userId, tokenRoles, passwordVersion,
                                context.HttpContext.RequestAborted))
                                context.Fail("Account role, status or password changed.");
                        }
                    };
                });

            // Redis Distributed Cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName  = "SmartPool:";
            });
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(
                ConfigurationOptions.Parse(configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Redis is not configured."))));
            services.AddOptions<StaffAttendanceQrSettings>()
                .Bind(configuration.GetSection(StaffAttendanceQrSettings.SectionName))
                .Validate(settings => settings.IsValid(), "StaffAttendanceQr settings must be positive.")
                .ValidateOnStart();
            services.AddScoped<IRegistrationOtpStore, RegistrationOtpStore>();
            services.AddScoped<IPasswordResetStore, PasswordResetStore>();

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
            services.AddScoped<IUserRoleMembershipService, UserRoleMembershipService>();
            services.AddScoped<IRefreshTokenService, RefreshTokenService>();
            services.AddScoped<IGoogleIdTokenVerifier, GoogleIdTokenVerifier>();

            // Identity of the authenticated caller in the current HTTP request
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();
            services.AddScoped<IRolePermissionReader, RolePermissionReader>();

            // SignalR — real-time notifications
            services.AddSignalR();
            services.AddScoped<INotificationService, SignalRNotificationService>();

            // Cache Service wrapper
            services.AddScoped<ICacheService, RedisCacheService>();
            services.AddScoped<IStaffAttendanceSessionStore, RedisStaffAttendanceSessionStore>();

            // Repository & Unit of Work
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUserOperations, UserOperations>();
            services.AddScoped<IRolePermissionOperations, RolePermissionOperations>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IServiceOperations, ServiceOperations>();
            services.AddScoped<IEquipmentOperations, EquipmentOperations>();
            services.AddScoped<IMaintenanceOperations, MaintenanceOperations>();
            services.AddHostedService<MaintenanceReminderWorker>();
            services.AddScoped<IStaffOperations, StaffOperations>();
            services.AddScoped<ISwimClassOperations, SwimClassOperations>();
            services.AddScoped<IPoolAccessOperations, PoolAccessOperations>();
            services.AddScoped<IVoucherOperations, VoucherOperations>();
            services.AddScoped<ITicketTypeOperations, SmartPool.Infrastructure.Persistence.Repositories.Tickets.TicketTypeOperations>();
            services.AddScoped<IPaymentOperations, SmartPool.Infrastructure.Persistence.Repositories.Payments.PaymentOperations>();
            services.AddScoped<IOrderPaymentOperations, SmartPool.Infrastructure.Persistence.Repositories.Payments.OrderPaymentOperations>();
            services.AddHostedService<ExpiredOnlineOrderWorker>();

            return services;
        }
    }
}
