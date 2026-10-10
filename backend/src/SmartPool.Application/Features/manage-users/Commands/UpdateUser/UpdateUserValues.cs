namespace SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;

public sealed record UpdateUserValues(
    Guid Id,
    string Email,
    string Phone,
    string FullName,
    DateOnly? DateOfBirth,
    string? Address);
