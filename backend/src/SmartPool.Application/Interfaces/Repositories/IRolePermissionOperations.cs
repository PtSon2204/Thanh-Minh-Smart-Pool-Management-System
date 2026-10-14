using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IRolePermissionOperations
{
    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken);
    Task<Role?> GetRoleAsync(Guid id, CancellationToken cancellationToken);
    Task<Role?> UpdatePermissionsAsync(Guid id, Func<Role, string> change, CancellationToken cancellationToken);
}
