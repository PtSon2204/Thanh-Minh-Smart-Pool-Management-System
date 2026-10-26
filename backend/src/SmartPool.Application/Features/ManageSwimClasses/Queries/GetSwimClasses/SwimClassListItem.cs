namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;

public sealed record SwimClassListItem(
    Guid Id,
    string Name,
    string? Description,
    int Capacity,
    decimal Fee,
    Guid CoachUserId,
    string? CoachName,
    DateTime RegistrationStartAt,
    DateTime RegistrationEndAt,
    string Status,
    int SessionCount,
    int ConfirmedStudentCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
