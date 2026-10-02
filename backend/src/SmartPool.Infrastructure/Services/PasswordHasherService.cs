using SmartPool.Domain.Entities;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Services;

public sealed class PasswordHasherService : IPasswordHasher
{
    public string HashPassword(User user, string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(User user, string passwordHash, string password)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}