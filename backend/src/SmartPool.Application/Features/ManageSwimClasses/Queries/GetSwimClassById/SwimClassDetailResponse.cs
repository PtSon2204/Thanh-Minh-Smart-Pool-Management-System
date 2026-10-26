namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;

public sealed record SwimClassDetailResponse(
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
    Guid CreatedByUserId,
    int ConfirmedStudentCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<SwimClassSessionDetail> Sessions);

public sealed record SwimClassSessionDetail(
    Guid Id,
    Guid CoachUserId,
    string? CoachName,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    string? CancelReason);
