using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ActivateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<CreateClassSessionsResponse> CreateSessionsAsync(
        CreateClassSessionsCommand request,
        Guid coachUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(request.ClassId, cancellationToken);
        await LockPoolScheduleAsync(cancellationToken);

        var swimClass = await context.SwimClasses
            .FirstOrDefaultAsync(item => item.Id == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");

        EnsureClassOwner(swimClass, coachUserId);

        if (swimClass.Status is not ("Draft" or "Active"))
            throw new SwimClassStateConflictException("Chỉ có thể thêm buổi học cho lớp Draft hoặc Active.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiredStarts = request.Sessions
            .Select((item, index) => (item, index))
            .Where(entry => entry.item.StartsAt.UtcDateTime <= now)
            .Select(entry => new ValidationFailure($"Sessions[{entry.index}].StartsAt",
                "Buổi học phải bắt đầu trong tương lai."))
            .ToList();
        if (expiredStarts.Count != 0) throw new ValidationException(expiredStarts);

        var proposed = request.Sessions.Select((item, index) => new ProposedClassSession(
            index,
            item.CoachUserId ?? swimClass.CoachUserId,
            item.StartsAt.UtcDateTime,
            item.EndsAt.UtcDateTime)).ToArray();

        if (swimClass.Status == "Active" &&
            proposed.Any(item => item.StartsAt < swimClass.RegistrationEndAt))
            throw new SwimClassStateConflictException(
                "Buổi học của lớp Active không được bắt đầu trước hạn kết thúc đăng ký.");

        var coachIds = proposed.Select(item => item.CoachUserId).Distinct().Order().ToArray();
        foreach (var coachId in coachIds)
            await LockCoachAsync(coachId, cancellationToken);

        var eligibleCoachIds = await EligibleCoaches()
            .Where(user => coachIds.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        var eligibleCoaches = eligibleCoachIds.ToHashSet();
        var invalidCoaches = proposed
            .Where(item => !eligibleCoaches.Contains(item.CoachUserId))
            .Select(item => new ValidationFailure($"Sessions[{item.Index}].CoachUserId",
                "Coach phải có tài khoản hoạt động và quyền COACH."))
            .ToList();
        if (invalidCoaches.Count != 0) throw new ValidationException(invalidCoaches);

        var earliestStart = proposed.Min(item => item.StartsAt);
        var latestEnd = proposed.Max(item => item.EndsAt);
        var storedSessions = await context.ClassSessions.AsNoTracking()
            .Where(item => item.Status != "Cancelled" &&
                item.StartsAt < latestEnd && item.EndsAt > earliestStart)
            .Select(item => new ExistingClassSession(
                item.Id, item.ClassId, item.CoachUserId, item.StartsAt, item.EndsAt))
            .ToListAsync(cancellationToken);

        var conflicts = ClassSessionConflictDetector.Find(request.ClassId, proposed, storedSessions);
        if (conflicts.Count != 0)
            throw new ClassSessionConflictException("Một hoặc nhiều buổi học bị trùng lịch.", conflicts);

        var sessions = proposed.Select(item => new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = request.ClassId,
            CoachUserId = item.CoachUserId,
            StartsAt = item.StartsAt,
            EndsAt = item.EndsAt,
            Status = "Scheduled",
            CreatedAt = now,
            UpdatedAt = now
        }).ToArray();

        context.ClassSessions.AddRange(sessions);
        swimClass.UpdatedAt = NextVersionTimestamp(now, swimClass.UpdatedAt);
        var notifications = swimClass.Status == "Active"
            ? await AddClassNotificationsAsync(request.ClassId, coachIds,
                "Lịch lớp dạy bơi thay đổi",
                $"Lịch học của lớp {swimClass.Name} đã có buổi mới. Vui lòng xem lại thời khóa biểu.",
                "swim_class_session_updated", now, cancellationToken)
            : [];
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await PublishClassNotificationsAsync(request.ClassId, notifications);

        return new CreateClassSessionsResponse(request.ClassId,
            sessions.Select(item => new ClassSessionResponse(
                item.Id, item.ClassId, item.CoachUserId, item.StartsAt,
                item.EndsAt, item.Status, item.CreatedAt, item.UpdatedAt)).ToArray());
    }

    public async Task<ActivateSwimClassResponse> ActivateAsync(
        Guid classId,
        Guid coachUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(classId, cancellationToken);

        var swimClass = await context.SwimClasses
            .FirstOrDefaultAsync(item => item.Id == classId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");

        EnsureClassOwner(swimClass, coachUserId);

        if (swimClass.Status != "Draft")
            throw new SwimClassStateConflictException("Chỉ có thể kích hoạt lớp đang ở trạng thái Draft.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var upcomingSessions = await context.ClassSessions.AsNoTracking()
            .Where(item => item.ClassId == classId &&
                item.Status == "Scheduled" && item.StartsAt > now && item.EndsAt > item.StartsAt)
            .Select(item => new { item.CoachUserId, item.StartsAt })
            .ToArrayAsync(cancellationToken);
        var upcomingCoachIds = upcomingSessions.Select(item => item.CoachUserId).Distinct().ToArray();
        var hasValidSession = upcomingCoachIds.Length != 0 &&
            await EligibleCoaches().AnyAsync(
                user => upcomingCoachIds.Contains(user.Id), cancellationToken);
        if (!hasValidSession)
            throw new SwimClassStateConflictException(
                "Lớp cần ít nhất một buổi học sắp tới với Coach đang hoạt động trước khi kích hoạt.");

        if (swimClass.RegistrationEndAt > upcomingSessions.Min(item => item.StartsAt))
            throw new SwimClassStateConflictException(
                "Hạn đăng ký phải kết thúc không muộn hơn buổi học đầu tiên.");

        swimClass.Status = "Active";
        swimClass.UpdatedAt = NextVersionTimestamp(now, swimClass.UpdatedAt);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ActivateSwimClassResponse(swimClass.Id, swimClass.Status, swimClass.UpdatedAt);
    }

    private async Task LockClassAsync(Guid classId, CancellationToken cancellationToken)
    {
        var key = $"swim-class:{classId:D}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

    private async Task LockCoachAsync(Guid coachId, CancellationToken cancellationToken)
    {
        var key = $"swim-coach:{coachId:D}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

    private async Task LockPoolScheduleAsync(CancellationToken cancellationToken)
    {
        const string key = "swim-class:pool-schedule";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

}
