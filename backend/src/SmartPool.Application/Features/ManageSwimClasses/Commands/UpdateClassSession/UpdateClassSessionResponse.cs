namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;

public sealed record UpdateClassSessionResponse(
    Guid Id,
    Guid ClassId,
    Guid CoachUserId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
