namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;

public sealed record CreateSwimClassResponse(
    Guid Id,
    string Name,
    string? Description,
    int Capacity,
    decimal Fee,
    Guid CoachUserId,
    DateTime RegistrationStartAt,
    DateTime RegistrationEndAt,
    string Status,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
