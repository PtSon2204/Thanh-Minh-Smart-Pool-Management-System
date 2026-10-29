using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Services;

public sealed class RolePermissionReader(SmartPoolDbContext database) : IRolePermissionReader
{
    public async Task<string?> ReadCurrentPermissionsAsync(
        Guid userId, string tokenRole, CancellationToken cancellationToken)
    {
        var account = await database.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.IsDeleted != true &&
                user.Status != null && EF.Functions.ILike(user.Status, "ACTIVE") &&
                user.Role != null && user.Role.IsDeleted != true)
            .Select(user => new { RoleName = user.Role!.Name, user.Role.Permissions,
                user.IsCoach })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null) return null;
        if (string.Equals(account.RoleName, tokenRole, StringComparison.OrdinalIgnoreCase))
            return account.Permissions;
        if (!account.IsCoach || !string.Equals(account.RoleName, "CUSTOMER",
                StringComparison.OrdinalIgnoreCase) || !string.Equals(tokenRole, "COACH",
                StringComparison.OrdinalIgnoreCase)) return null;
        return await database.Roles.AsNoTracking()
            .Where(item => item.Name == "COACH" && item.IsDeleted != true)
            .Select(item => item.Permissions)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
