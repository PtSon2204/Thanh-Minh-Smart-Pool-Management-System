using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<RegisterForSwimClassResponse> RegisterAsync(
        Guid classId, Guid customerUserId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(classId, cancellationToken);

        var swimClass = await context.SwimClasses
            .SingleOrDefaultAsync(item => item.Id == classId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (swimClass.Status != "Active" || now < swimClass.RegistrationStartAt ||
            now >= swimClass.RegistrationEndAt)
            throw new SwimClassStateConflictException("Lớp không trong thời gian nhận đăng ký.");
        if (swimClass.CreatedByUserId == customerUserId ||
            await context.ClassSessions.AnyAsync(item => item.ClassId == classId &&
                item.Status == "Scheduled" && item.CoachUserId == customerUserId, cancellationToken))
            throw new SwimClassStateConflictException("Coach không thể đăng ký lớp mình tạo hoặc trực tiếp dạy.");
        if (!await context.ClassSessions.AnyAsync(item => item.ClassId == classId &&
                item.Status == "Scheduled" && item.StartsAt > now, cancellationToken))
            throw new SwimClassStateConflictException("Lớp không còn buổi học sắp tới.");

        var registration = await context.ClassRegistrations.SingleOrDefaultAsync(
            item => item.ClassId == classId && item.CustomerUserId == customerUserId,
            cancellationToken);
        ClassRegistrationRequestPolicy.EnsureCanRequest(registration);

        var occupied = await context.ClassRegistrations.AsNoTracking()
            .CountAsync(item => item.ClassId == classId && item.Status == "Confirmed",
                cancellationToken);
        if (occupied >= swimClass.Capacity)
            throw new SwimClassStateConflictException("Lớp đã hết chỗ đăng ký.");

        var updatedAt = registration is null
            ? now : NextVersionTimestamp(now, registration.UpdatedAt);
        registration ??= new ClassRegistration
        {
            Id = Guid.NewGuid(), ClassId = classId,
            CustomerUserId = customerUserId, CreatedAt = now
        };
        if (context.Entry(registration).State == EntityState.Detached)
            context.ClassRegistrations.Add(registration);
        ClassRegistrationRequestPolicy.PreparePendingApproval(registration, now, updatedAt);

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = swimClass.CreatedByUserId,
            Title = "Có yêu cầu tham gia lớp mới",
            Content = $"Một Customer đã gửi yêu cầu tham gia lớp {swimClass.Name}.",
            Type = "swim_class_registration_requested",
            IsRead = false,
            CreatedAt = now
        };
        context.Notifications.Add(notification);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishClassNotificationsAsync(classId, [notification]);
        return new RegisterForSwimClassResponse(registration.Id, classId,
            registration.Status, registration.RegisteredAt, registration.ConfirmedAt);
    }
}
