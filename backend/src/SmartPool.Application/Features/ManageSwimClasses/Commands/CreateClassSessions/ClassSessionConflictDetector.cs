namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;

public sealed record ProposedClassSession(
    int Index, Guid CoachUserId, DateTime StartsAt, DateTime EndsAt);

public sealed record ExistingClassSession(
    Guid Id, Guid ClassId, Guid CoachUserId, DateTime StartsAt, DateTime EndsAt);

public static class ClassSessionConflictDetector
{
    public static IReadOnlyList<ClassSessionConflict> Find(
        Guid classId,
        IReadOnlyList<ProposedClassSession> proposed,
        IReadOnlyList<ExistingClassSession> existingSessions)
    {
        var conflicts = new List<ClassSessionConflict>();
        for (var index = 0; index < proposed.Count; index++)
        {
            var current = proposed[index];
            for (var earlierIndex = 0; earlierIndex < index; earlierIndex++)
            {
                var earlier = proposed[earlierIndex];
                if (Overlaps(current.StartsAt, current.EndsAt, earlier.StartsAt, earlier.EndsAt))
                    conflicts.Add(new ClassSessionConflict(current.Index, current.CoachUserId,
                        current.StartsAt, current.EndsAt, "request_overlap",
                        ConflictingRequestIndex: earlier.Index));
            }

            foreach (var existing in existingSessions)
            {
                if (Overlaps(current.StartsAt, current.EndsAt, existing.StartsAt, existing.EndsAt))
                    conflicts.Add(new ClassSessionConflict(current.Index, current.CoachUserId,
                        current.StartsAt, current.EndsAt,
                        existing.ClassId == classId ? "class_overlap" :
                            existing.CoachUserId == current.CoachUserId ? "coach_overlap" : "pool_overlap",
                        ConflictingSessionId: existing.Id));
            }
        }

        return conflicts;
    }

    private static bool Overlaps(DateTime firstStart, DateTime firstEnd, DateTime secondStart, DateTime secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;
}
