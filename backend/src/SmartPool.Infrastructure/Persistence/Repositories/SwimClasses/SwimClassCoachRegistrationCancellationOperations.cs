using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassRegistration;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<CancelClassRegistrationResponse> CancelClassRegistrationAsync(
        CancelClassRegistrationCommand request, Guid coachUserId, CancellationToken cancellationToken)
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

        if (registration.UpdatedAt != request.ExpectedUpdatedAt.UtcDateTime)
            throw new SwimClassStateConflictException(
                "Đăng ký đã được cập nhật. Vui lòng tải lại trước khi hủy.");
        if (registration.Status != "Confirmed")
            throw new SwimClassStateConflictException("Chỉ có thể hủy đăng ký Confirmed.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reason = request.Reason.Trim();
        registration.Status = "Cancelled";
        registration.CancelReason = reason;
        registration.CancelledAt = now;
        registration.CancelledByUserId = coachUserId;
        registration.UpdatedAt = NextVersionTimestamp(now, registration.UpdatedAt);

        var notification = new Notification
        {
            Id = Guid.NewGuid(), UserId = registration.CustomerUserId,
            Title = "Đăng ký lớp đã được hủy",
            Content = $"Coach đã hủy đăng ký lớp {swimClass.Name}. Lý do: {reason}",
            Type = "swim_class_registration_cancelled",
            IsRead = false, CreatedAt = now
        };
        context.Notifications.Add(notification);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishClassNotificationsAsync(request.ClassId, [notification]);
        return new CancelClassRegistrationResponse(registration.Id, registration.ClassId,
            registration.Status, reason, coachUserId, now, registration.UpdatedAt);
    }
}
