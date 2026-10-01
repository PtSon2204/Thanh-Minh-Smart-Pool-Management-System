using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Services;

public interface IPasswordHasher
{
    string HashPassword(User user, string password);

    bool VerifyPassword(User user, string passwordHash, string password);
}
