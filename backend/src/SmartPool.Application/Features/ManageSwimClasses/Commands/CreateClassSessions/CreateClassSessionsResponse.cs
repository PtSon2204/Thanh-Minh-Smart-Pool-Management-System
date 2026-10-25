namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;

public sealed record CreateClassSessionsResponse(
    Guid ClassId,
    IReadOnlyList<ClassSessionResponse> Sessions);

public sealed record ClassSessionResponse(
    Guid Id,
    Guid ClassId,
    Guid CoachUserId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
