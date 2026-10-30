using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<UpdateSwimClassResponse> UpdateAsync(
        UpdateSwimClassCommand request,
        Guid coachUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(request.ClassId, cancellationToken);

        var swimClass = await context.SwimClasses
            .SingleOrDefaultAsync(item => item.Id == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");

        EnsureClassOwner(swimClass, coachUserId);

        if (swimClass.UpdatedAt != request.ExpectedUpdatedAt.UtcDateTime)
            throw new SmartPool.Application.Features.ManageSwimClasses.SwimClassStateConflictException(
                "Lớp đã được người khác cập nhật. Vui lòng tải lại trước khi lưu.");

        var registrations = context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == request.ClassId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var occupiedPlaces = await registrations.CountAsync(item => item.Status == "Confirmed",
            cancellationToken);
        // Keep the published fee stable once someone has registered.
        var hasRegistration = request.Fee != swimClass.Fee &&
            await registrations.AnyAsync(cancellationToken);
        SwimClassUpdatePolicy.EnsureCapacityAndFee(
            request.Capacity, occupiedPlaces, swimClass.Fee, request.Fee, hasRegistration);

        var name = request.Name.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null : request.Description.Trim();
        var registrationStartAt = request.RegistrationStartAt.UtcDateTime;
        var registrationEndAt = request.RegistrationEndAt.UtcDateTime;
        if (swimClass.Status == "Active")
        {
            var firstUpcomingSession = await context.ClassSessions.AsNoTracking()
                .Where(item => item.ClassId == request.ClassId && item.Status == "Scheduled" && item.StartsAt > now)
                .OrderBy(item => item.StartsAt)
                .Select(item => (DateTime?)item.StartsAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (firstUpcomingSession is null || registrationEndAt > firstUpcomingSession.Value)
                throw new SmartPool.Application.Features.ManageSwimClasses.SwimClassStateConflictException(
                    "Hạn đăng ký phải kết thúc không muộn hơn buổi học sắp tới đầu tiên.");
        }
        var changed = swimClass.Name != name || swimClass.Description != description ||
            swimClass.Capacity != request.Capacity || swimClass.Fee != request.Fee ||
            swimClass.RegistrationStartAt != registrationStartAt ||
            swimClass.RegistrationEndAt != registrationEndAt;

        swimClass.Name = name;
        swimClass.Description = description;
        swimClass.Capacity = request.Capacity;
        swimClass.Fee = request.Fee;
        swimClass.RegistrationStartAt = registrationStartAt;
        swimClass.RegistrationEndAt = registrationEndAt;
        swimClass.UpdatedAt = NextVersionTimestamp(now, swimClass.UpdatedAt);

        var notifications = new List<SmartPool.Domain.Entities.Notification>();
        if (changed)
        {
            var futureCoachIds = await context.ClassSessions.AsNoTracking()
                .Where(item => item.ClassId == request.ClassId &&
                    item.Status != "Cancelled" && item.StartsAt > now)
                .Select(item => item.CoachUserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            futureCoachIds.Add(swimClass.CoachUserId);
            notifications = await AddClassNotificationsAsync(
                request.ClassId, futureCoachIds,
                "Thông tin lớp dạy bơi thay đổi",
                $"Thông tin lớp {name} đã được cập nhật. Vui lòng xem lại chi tiết lớp.",
                "swim_class_updated", now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await PublishClassNotificationsAsync(request.ClassId, notifications);

        return new UpdateSwimClassResponse(
            swimClass.Id, swimClass.Name, swimClass.Description, swimClass.Capacity,
            swimClass.Fee, swimClass.CoachUserId, swimClass.RegistrationStartAt,
            swimClass.RegistrationEndAt, swimClass.Status, swimClass.CreatedByUserId,
            swimClass.CreatedAt, swimClass.UpdatedAt);
    }
}
