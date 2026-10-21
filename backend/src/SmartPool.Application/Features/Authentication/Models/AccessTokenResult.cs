using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Authentication.Models;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
