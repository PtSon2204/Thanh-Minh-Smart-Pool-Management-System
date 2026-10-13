using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Roles;

public sealed class RolePermissionOperations(SmartPoolDbContext database) : IRolePermissionOperations
{
    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken) =>
        await database.Roles.AsNoTracking()
            .Where(role => role.IsDeleted != true)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

    public Task<Role?> GetRoleAsync(Guid id, CancellationToken cancellationToken) =>
        database.Roles.AsNoTracking()
            .SingleOrDefaultAsync(role => role.Id == id && role.IsDeleted != true, cancellationToken);

    public async Task<Role?> UpdatePermissionsAsync(
        Guid id, Func<Role, string> change, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var role = (await database.Roles
            .FromSqlInterpolated($"SELECT * FROM smart_pool.roles WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        if (role is null || role.IsDeleted == true) return null;

        var updated = change(role);
        if (!string.Equals(role.Permissions, updated, StringComparison.Ordinal))
        {
            role.Permissions = updated;
            role.UpdatedAt = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return role;
    }
}
