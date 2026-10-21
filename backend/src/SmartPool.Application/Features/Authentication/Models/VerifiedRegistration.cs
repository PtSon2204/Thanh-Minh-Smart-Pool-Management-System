namespace SmartPool.Application.Features.Authentication.Models;

public sealed record VerifiedRegistration(
    string Username,
    string Email,
    string Phone,
    string PasswordHash,
    string FullName,
    DateOnly? DateOfBirth,
    string? Address)
{
    public override string ToString() => "VerifiedRegistration { secrets = [REDACTED] }";
}
