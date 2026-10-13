namespace SmartPool.Application.Interfaces.Services;

public interface IRolePermissionReader
{
    Task<string?> ReadCurrentPermissionsAsync(Guid userId, string tokenRole, CancellationToken cancellationToken);
}
