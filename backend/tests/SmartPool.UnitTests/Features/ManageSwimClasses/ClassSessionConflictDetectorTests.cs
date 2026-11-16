using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ClassSessionConflictDetectorTests
{
    private static readonly DateTime BaseTime = new(2026, 11, 20, 1, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "UTCID01 (N) - Hai buổi sát nhau không giao thời gian → không xung đột")]
    public void Find_AdjacentSessions_ReturnsEmpty()
    {
        var classId = Guid.NewGuid();
        var coach = Guid.NewGuid();
        var proposed = new[] { Session(0, coach, 0, 60), Session(1, coach, 60, 120) };

        Assert.Empty(ClassSessionConflictDetector.Find(classId, proposed, []));
    }

    [Fact(DisplayName = "UTCID02 (A) - Hai buổi trong request chồng giờ → request_overlap")]
    public void Find_RequestOverlap_ReportsEarlierIndex()
    {
        var coach = Guid.NewGuid();
        var proposed = new[] { Session(0, coach, 0, 60), Session(1, coach, 30, 90) };

        var conflict = Assert.Single(ClassSessionConflictDetector.Find(Guid.NewGuid(), proposed, []));

        Assert.Equal("request_overlap", conflict.Reason);
        Assert.Equal(1, conflict.SessionIndex);
        Assert.Equal(0, conflict.ConflictingRequestIndex);
        Assert.Equal(coach, conflict.CoachUserId);
    }

    [Fact(DisplayName = "UTCID03 (A) - Buổi mới trùng buổi đã lưu của cùng lớp → class_overlap")]
    public void Find_SameClassOverlap_ReportsStoredSession()
    {
        var classId = Guid.NewGuid();
        var stored = Existing(Guid.NewGuid(), classId, Guid.NewGuid(), 0, 60);

        var conflict = Assert.Single(ClassSessionConflictDetector.Find(classId,
            [Session(0, Guid.NewGuid(), 30, 90)], [stored]));

        Assert.Equal("class_overlap", conflict.Reason);
        Assert.Equal(stored.Id, conflict.ConflictingSessionId);
    }

    [Fact(DisplayName = "UTCID04 (A) - Coach đã dạy lớp khác cùng giờ → coach_overlap")]
    public void Find_CoachOverlap_ReportsStoredSession()
    {
        var classId = Guid.NewGuid();
        var coach = Guid.NewGuid();
        var stored = Existing(Guid.NewGuid(), Guid.NewGuid(), coach, 0, 60);

        var conflict = Assert.Single(ClassSessionConflictDetector.Find(classId,
            [Session(0, coach, 30, 90)], [stored]));

        Assert.Equal("coach_overlap", conflict.Reason);
        Assert.Equal(stored.Id, conflict.ConflictingSessionId);
    }

    [Fact(DisplayName = "UTCID05 (B) - Lớp khác và Coach khác có giờ trùng → không xung đột")]
    public void Find_UnrelatedClassAndCoach_IgnoresOverlap()
    {
        var conflict = ClassSessionConflictDetector.Find(Guid.NewGuid(),
            [Session(0, Guid.NewGuid(), 30, 90)],
            [Existing(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, 60)]);

        Assert.Empty(conflict);
    }

    [Fact(DisplayName = "UTCID06 (A) - Vừa trùng request vừa trùng lịch Coach → trả đủ hai lý do")]
    public void Find_MultipleOverlaps_ReturnsAllReasons()
    {
        var coach = Guid.NewGuid();
        var proposed = new[] { Session(0, coach, 0, 60), Session(1, coach, 30, 90) };
        var stored = Existing(Guid.NewGuid(), Guid.NewGuid(), coach, 45, 75);

        var conflicts = ClassSessionConflictDetector.Find(Guid.NewGuid(), proposed, [stored]);

        Assert.Contains(conflicts, x => x.Reason == "request_overlap" && x.SessionIndex == 1);
        Assert.Contains(conflicts, x => x.Reason == "coach_overlap" && x.ConflictingSessionId == stored.Id);
        Assert.Equal(3, conflicts.Count);
    }

    private static ProposedClassSession Session(int index, Guid coach, int startMinutes, int endMinutes) =>
        new(index, coach, BaseTime.AddMinutes(startMinutes), BaseTime.AddMinutes(endMinutes));

    private static ExistingClassSession Existing(Guid id, Guid classId, Guid coach, int startMinutes, int endMinutes) =>
        new(id, classId, coach, BaseTime.AddMinutes(startMinutes), BaseTime.AddMinutes(endMinutes));
}
