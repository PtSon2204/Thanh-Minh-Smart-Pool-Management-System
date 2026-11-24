using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    private async Task LockCoachesForClassScheduleAsync(
        Guid classId, Guid teachingCoachId, CancellationToken cancellationToken)
    {
        var studentCoachIds = await context.ClassRegistrations.AsNoTracking()
            .Where(registration => registration.ClassId == classId &&
                registration.Status == "Confirmed")
            .Select(registration => registration.CustomerUserId)
            .ToListAsync(cancellationToken);

        // All schedule writers take coach locks in one order, including coaches
        // attending this class, so concurrent edits cannot cross their schedules.
        foreach (var coachId in studentCoachIds.Append(teachingCoachId).Distinct().Order())
            await LockCoachAsync(coachId, cancellationToken);
    }

    private async Task EnsureNoTeachingConflictForStudentAsync(
        Guid classId, Guid studentUserId, CancellationToken cancellationToken)
    {
        // Use the same coach lock as session creation and updates. Otherwise a new
        // teaching session could be saved between this check and confirmation.
        await LockCoachAsync(studentUserId, cancellationToken);

        var conflicts = await context.ClassSessions.AsNoTracking().AnyAsync(taught =>
            taught.CoachUserId == studentUserId && taught.Status == "Scheduled" &&
            context.ClassSessions.Any(lesson =>
                lesson.ClassId == classId && lesson.Status == "Scheduled" &&
                lesson.StartsAt < taught.EndsAt && taught.StartsAt < lesson.EndsAt),
            cancellationToken);

        if (conflicts)
            throw new SwimClassStateConflictException(
                "Lịch học của bạn trùng với buổi học bạn đang phụ trách ở lớp khác.");
    }

    private async Task EnsureNoConfirmedClassConflictForStudentAsync(
        Guid classId, Guid studentUserId, CancellationToken cancellationToken)
    {
        // The caller holds this student's schedule lock, including when two
        // different class owners review requests at the same time.
        var conflicts = await context.ClassRegistrations.AsNoTracking().AnyAsync(registration =>
            registration.CustomerUserId == studentUserId &&
            registration.ClassId != classId && registration.Status == "Confirmed" &&
            context.ClassSessions.Any(otherLesson =>
                otherLesson.ClassId == registration.ClassId &&
                otherLesson.Status == "Scheduled" &&
                context.ClassSessions.Any(lesson =>
                    lesson.ClassId == classId && lesson.Status == "Scheduled" &&
                    lesson.StartsAt < otherLesson.EndsAt &&
                    otherLesson.StartsAt < lesson.EndsAt)),
            cancellationToken);

        if (conflicts)
            throw new SwimClassStateConflictException(
                "Lịch học của lớp này trùng với lớp khác bạn đã được xác nhận tham gia.");
    }

    private async Task EnsureNoConfirmedStudentConflictForCoachAsync(
        Guid coachUserId, DateTime startsAt, DateTime endsAt,
        CancellationToken cancellationToken)
    {
        var conflicts = await context.ClassRegistrations.AsNoTracking().AnyAsync(registration =>
            registration.CustomerUserId == coachUserId && registration.Status == "Confirmed" &&
            context.ClassSessions.Any(lesson =>
                lesson.ClassId == registration.ClassId && lesson.Status == "Scheduled" &&
                lesson.StartsAt < endsAt && startsAt < lesson.EndsAt),
            cancellationToken);

        if (conflicts)
            throw new SwimClassStateConflictException(
                "Lịch dạy trùng với lớp bạn đã đăng ký và được xác nhận tham gia.");
    }

    private async Task EnsureNoTeachingConflictForConfirmedStudentsAsync(
        Guid classId, DateTime startsAt, DateTime endsAt,
        CancellationToken cancellationToken)
    {
        var conflicts = await context.ClassSessions.AsNoTracking().AnyAsync(taught =>
            taught.Status == "Scheduled" &&
            taught.StartsAt < endsAt && startsAt < taught.EndsAt &&
            context.ClassRegistrations.Any(registration =>
                registration.ClassId == classId && registration.Status == "Confirmed" &&
                registration.CustomerUserId == taught.CoachUserId),
            cancellationToken);

        if (conflicts)
            throw new SwimClassStateConflictException(
                "Lịch học mới trùng với lịch dạy của học viên đã được xác nhận.");
    }

    private async Task EnsureNoConfirmedStudentClassConflictAsync(
        Guid classId, DateTime startsAt, DateTime endsAt,
        CancellationToken cancellationToken)
    {
        var conflicts = await context.ClassRegistrations.AsNoTracking().AnyAsync(student =>
            student.ClassId == classId && student.Status == "Confirmed" &&
            context.ClassRegistrations.Any(otherRegistration =>
                otherRegistration.CustomerUserId == student.CustomerUserId &&
                otherRegistration.ClassId != classId &&
                otherRegistration.Status == "Confirmed" &&
                context.ClassSessions.Any(otherLesson =>
                    otherLesson.ClassId == otherRegistration.ClassId &&
                    otherLesson.Status == "Scheduled" &&
                    otherLesson.StartsAt < endsAt && startsAt < otherLesson.EndsAt)),
            cancellationToken);

        if (conflicts)
            throw new SwimClassStateConflictException(
                "Lịch học mới trùng với lớp khác của học viên đã được xác nhận.");
    }
}
