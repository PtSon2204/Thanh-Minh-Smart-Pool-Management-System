namespace SmartPool.Application.Features.Authentication.Models;

public sealed record RefreshTokenResult(string Token, DateTime ExpiresAtUtc);
