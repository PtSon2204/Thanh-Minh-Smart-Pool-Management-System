using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<ReviewClassRegistrationResponse> ReviewClassRegistrationAsync(
        ReviewClassRegistrationCommand request, Guid coachUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(request.ClassId, cancellationToken);

        var swimClass = await context.SwimClasses.SingleOrDefaultAsync(
            item => item.Id == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");
        EnsureClassOwner(swimClass, coachUserId);

        var registration = await context.ClassRegistrations.SingleOrDefaultAsync(
            item => item.Id == request.RegistrationId && item.ClassId == request.ClassId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đăng ký của lớp.");
        ClassRegistrationReviewPolicy.EnsurePending(registration.Status,
            registration.UpdatedAt, request.ExpectedUpdatedAt);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var accepted = string.Equals(request.Decision.Trim(), "Accepted",
            StringComparison.OrdinalIgnoreCase);
        if (accepted)
        {
            var hasFutureSession = await context.ClassSessions.AsNoTracking()
                .AnyAsync(item => item.ClassId == request.ClassId &&
                    item.Status == "Scheduled" && item.StartsAt > now,
                    cancellationToken);
            var confirmedCount = await context.ClassRegistrations.AsNoTracking()
                .CountAsync(item => item.ClassId == request.ClassId &&
                    item.Status == "Confirmed", cancellationToken);
            ClassRegistrationReviewPolicy.EnsureCanAccept(swimClass.Status,
                hasFutureSession, confirmedCount, swimClass.Capacity);
        }

        var note = string.IsNullOrWhiteSpace(request.ReviewNote)
            ? null : request.ReviewNote.Trim();
        registration.Status = accepted ? "Confirmed" : "Rejected";
        registration.ConfirmedAt = accepted ? now : null;
        registration.ReviewedByUserId = coachUserId;
        registration.ReviewedAt = now;
        registration.ReviewNote = note;
        registration.UpdatedAt = NextVersionTimestamp(now, registration.UpdatedAt);

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = registration.CustomerUserId,
            Title = accepted ? "Yêu cầu tham gia lớp đã được chấp nhận" :
                "Yêu cầu tham gia lớp bị từ chối",
            Content = accepted
                ? $"Coach đã chấp nhận yêu cầu tham gia lớp {swimClass.Name} của bạn."
                : $"Coach đã từ chối yêu cầu tham gia lớp {swimClass.Name}. Lý do: {note}",
            Type = "swim_class_registration_reviewed",
            IsRead = false,
            CreatedAt = now
        };
        context.Notifications.Add(notification);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishClassNotificationsAsync(request.ClassId, [notification]);

        return new ReviewClassRegistrationResponse(registration.Id, registration.ClassId,
            registration.Status, coachUserId, now, note, registration.ConfirmedAt,
            registration.UpdatedAt);
    }
}
