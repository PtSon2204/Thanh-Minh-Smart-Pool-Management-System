namespace SmartPool.Application.Features.Profiles.Queries.GetMyProfile;

public sealed class GetMyProfileResponse
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string? Address { get; init; }
    public DateOnly? DateOfBirth { get; init; }
}
