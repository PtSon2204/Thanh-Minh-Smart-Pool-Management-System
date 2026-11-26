using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CompleteSwimClass;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<string> CompleteClassAsync(Guid classId, Guid coachUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(classId, cancellationToken);
        var swimClass = await context.SwimClasses.SingleOrDefaultAsync(item => item.Id == classId,
            cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");
        EnsureClassOwner(swimClass, coachUserId);
        if (swimClass.Status != "Active")
            throw new SwimClassStateConflictException("Chỉ có thể kết thúc lớp đang hoạt động.");
        var sessions = await context.ClassSessions.AsNoTracking()
            .Where(item => item.ClassId == classId && item.Status == "Scheduled")
            .Select(item => new { item.Id, item.EndsAt })
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        ClassCompletionPolicy.EnsureCanComplete(swimClass.Status,
            sessions.Select(item => item.EndsAt).ToArray(), false, false, now);
        var hasPendingRegistrations = await context.ClassRegistrations.AsNoTracking().AnyAsync(item =>
            item.ClassId == classId && item.Status == "PendingApproval", cancellationToken);
        var registrations = await context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == classId && item.Status == "Confirmed")
            .Select(item => new { item.Id, item.ConfirmedAt })
            .ToListAsync(cancellationToken);
        var attendances = await context.ClassAttendances.AsNoTracking()
            .Where(item => item.Session.ClassId == classId)
            .Select(item => new { item.SessionId, item.RegistrationId, item.Status })
            .ToListAsync(cancellationToken);
        var marked = attendances.Where(item => item.Status is "Verified" or "Absent" or "Approved")
            .Select(item => (item.SessionId, item.RegistrationId)).ToHashSet();
        var hasMissingAttendances = sessions.Any(session => registrations.Any(registration =>
                (registration.ConfirmedAt is null || registration.ConfirmedAt <= session.EndsAt) &&
                !marked.Contains((session.Id, registration.Id))));
        ClassCompletionPolicy.EnsureCanComplete(swimClass.Status,
            sessions.Select(item => item.EndsAt).ToArray(), hasPendingRegistrations, hasMissingAttendances, now);
        swimClass.Status = "Completed";
        swimClass.UpdatedAt = NextVersionTimestamp(now, swimClass.UpdatedAt);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return swimClass.Status;
    }
}
