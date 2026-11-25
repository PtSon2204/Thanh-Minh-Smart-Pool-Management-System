using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Seeding;

/// <summary>
/// Tạo tài khoản ADMIN khởi tạo từ biến môi trường (AdminSeed:Email &amp; AdminSeed:Password)
/// nếu chưa tồn tại trong DB. Bỏ qua an toàn khi không cấu hình.
/// </summary>
public static class AdminUserSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(AdminUserSeeder));

        var email = configuration["AdminSeed:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogDebug("[AdminUserSeeder] AdminSeed:Email hoặc AdminSeed:Password chưa cấu hình — bỏ qua.");
            return;
        }

        var username = configuration["AdminSeed:Username"]?.Trim();
        if (string.IsNullOrWhiteSpace(username))
            username = "admin";

        var fullName = configuration["AdminSeed:FullName"]?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = "System Administrator";

        var phone = configuration["AdminSeed:Phone"]?.Trim();
        if (string.IsNullOrWhiteSpace(phone))
            phone = null;

        var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var adminRole = await db.Roles
            .FirstOrDefaultAsync(
                r => r.IsDeleted != true && EF.Functions.ILike(r.Name, "ADMIN"),
                cancellationToken);

        if (adminRole is null)
        {
            logger.LogWarning("[AdminUserSeeder] Chưa tìm thấy role ADMIN trong DB — bỏ qua.");
            return;
        }

        var existingUser = await db.Users
            .FirstOrDefaultAsync(
                u => (u.Email != null && EF.Functions.ILike(u.Email, email)) ||
                     (u.Username != null && EF.Functions.ILike(u.Username, username)),
                cancellationToken);

        if (existingUser is not null)
        {
            logger.LogInformation("[AdminUserSeeder] Tài khoản admin đã tồn tại — bỏ qua.");
            return;
        }

        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            RoleId = adminRole.Id,
            IsCoach = false,
            Username = username,
            Email = email,
            Phone = phone,
            Status = "ACTIVE",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        var profile = new UserProfile
        {
            UserId = userId,
            FullName = fullName,
            UpdatedAt = now,
        };

        db.Users.Add(user);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[AdminUserSeeder] Đã tạo tài khoản ADMIN ban đầu ({Email}).", email);
    }
}
