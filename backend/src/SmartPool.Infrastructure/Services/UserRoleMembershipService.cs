using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Services;

public sealed class UserRoleMembershipService(SmartPoolDbContext context) : IUserRoleMembershipService
{
    public async Task<IReadOnlyList<string>> GetActiveRolesAsync(Guid userId, string primaryRole,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Users.AsNoTracking()
            .Where(item => item.Id == userId && item.IsDeleted != true &&
                item.Status != null && EF.Functions.ILike(item.Status, "ACTIVE"))
            .Select(item => new { item.IsCoach, PrimaryRole = item.Role != null ? item.Role.Name : null })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || !string.Equals(user.PrimaryRole, primaryRole,
                StringComparison.OrdinalIgnoreCase))
            return [];
        var coachEnabled = user.IsCoach &&
            string.Equals(primaryRole, "CUSTOMER", StringComparison.OrdinalIgnoreCase) &&
            await context.Roles.AsNoTracking()
            .AnyAsync(item => item.Name == "COACH" && item.IsDeleted != true, cancellationToken);
        return coachEnabled && !string.Equals(primaryRole, "COACH", StringComparison.OrdinalIgnoreCase)
            ? [primaryRole, "COACH"] : [primaryRole];
    }

    public async Task GrantCoachAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774201001)", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.users WHERE id = {userId} FOR UPDATE", cancellationToken);
        var user = await context.Users.Include(item => item.Role)
            .SingleOrDefaultAsync(item => item.Id == userId && item.IsDeleted != true,
                cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");
        if (user.Status is null || !user.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(user.Role?.Name, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ Customer đang hoạt động được cấp quyền Coach.");
        if (!await context.Roles.AnyAsync(item => item.Name == "COACH" &&
            item.IsDeleted != true, cancellationToken))
            throw new InvalidOperationException("Role COACH chưa khả dụng.");
        user.IsCoach = true;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeCoachAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774201001)", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.users WHERE id = {userId} FOR UPDATE", cancellationToken);
        var user = await context.Users.SingleOrDefaultAsync(item => item.Id == userId,
            cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");
        if (user.RoleId is { } roleId && await context.Roles.AsNoTracking().AnyAsync(item =>
                item.Id == roleId && item.Name == "COACH", cancellationToken))
            throw new InvalidOperationException("Không thể thu hồi COACH khi đây là vai trò chính.");
        if (await context.SwimClasses.AnyAsync(item => item.CreatedByUserId == userId &&
            (item.Status == "Draft" || item.Status == "Active"), cancellationToken))
            throw new InvalidOperationException("Coach phải ngừng các lớp đang quản lý trước khi thu hồi quyền.");
        user.IsCoach = false;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
