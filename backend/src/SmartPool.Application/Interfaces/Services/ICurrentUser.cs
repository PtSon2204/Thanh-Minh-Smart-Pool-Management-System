namespace SmartPool.Application.Interfaces.Services;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Role { get; }

    bool IsInRole(string role);
}
