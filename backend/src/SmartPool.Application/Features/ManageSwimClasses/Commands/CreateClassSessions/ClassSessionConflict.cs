namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;

public sealed record ClassSessionConflict(
    int SessionIndex,
    Guid CoachUserId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Reason,
    Guid? ConflictingSessionId = null,
    int? ConflictingRequestIndex = null);

public sealed class ClassSessionConflictException(
    string message,
    IReadOnlyList<ClassSessionConflict> conflicts) : Exception(message)
{
    public IReadOnlyList<ClassSessionConflict> Conflicts { get; } = conflicts;
}
