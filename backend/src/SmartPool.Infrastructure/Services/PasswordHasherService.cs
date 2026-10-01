using Microsoft.AspNetCore.Identity;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Services;

public sealed class PasswordHasherService : SmartPool.Application.Interfaces.Services.IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.IPasswordHasher<User> _hasher;

    public PasswordHasherService(Microsoft.AspNetCore.Identity.IPasswordHasher<User> hasher)
    {
        _hasher = hasher;
    }

    public string HashPassword(User user, string password)
    {
        return _hasher.HashPassword(user, password);
    }

    public bool VerifyPassword(User user, string passwordHash, string password)
    {
        return _hasher.VerifyHashedPassword(user, passwordHash, password)
            != PasswordVerificationResult.Failed;
    }
}
