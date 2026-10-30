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
        if (registration is { Status: "Confirmed" })
            throw new SwimClassStateConflictException("Bạn đã đăng ký lớp này.");
        if (registration is { Status: not "Cancelled" })
            throw new SwimClassStateConflictException("Đăng ký lớp hiện tại chưa thể tạo lại.");

        var occupied = await context.ClassRegistrations.AsNoTracking()
            .CountAsync(item => item.ClassId == classId && item.Status == "Confirmed",
                cancellationToken);
        if (occupied >= swimClass.Capacity)
            throw new SwimClassStateConflictException("Lớp đã hết chỗ đăng ký.");

        registration ??= new ClassRegistration
        {
            Id = Guid.NewGuid(), ClassId = classId,
            CustomerUserId = customerUserId, CreatedAt = now
        };
        if (context.Entry(registration).State == EntityState.Detached)
            context.ClassRegistrations.Add(registration);
        registration.Status = "Confirmed";
        registration.RegisteredAt = now;
        registration.ConfirmedAt = now;
        registration.CancelledAt = null;
        registration.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RegisterForSwimClassResponse(registration.Id, classId,
            registration.Status, registration.RegisteredAt, registration.ConfirmedAt.Value);
    }
}
