using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<UpdateSwimClassResponse> UpdateAsync(
        UpdateSwimClassCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockClassAsync(request.ClassId, cancellationToken);

        var swimClass = await context.SwimClasses
            .SingleOrDefaultAsync(item => item.Id == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");

        var eligibleCoach = await EligibleCoaches()
            .AnyAsync(employee => employee.UserId == request.CoachUserId, cancellationToken);
        if (!eligibleCoach)
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.CoachUserId),
                    "Coach phải là nhân viên đang làm việc, có tài khoản hoạt động và vai trò COACH.")
            });

        var registrations = context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == request.ClassId);
        var confirmedStudents = await registrations
            .CountAsync(item => item.Status == "Confirmed", cancellationToken);
        // Every class order is referenced by a registration, so any registration
        // locks the historical fee, including records with an attached order.
        var hasRegistration = request.Fee != swimClass.Fee &&
            await registrations.AnyAsync(cancellationToken);
        SwimClassUpdatePolicy.EnsureCapacityAndFee(
            request.Capacity, confirmedStudents, swimClass.Fee, request.Fee, hasRegistration);

        swimClass.Name = request.Name.Trim();
        swimClass.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null : request.Description.Trim();
        swimClass.Capacity = request.Capacity;
        swimClass.Fee = request.Fee;
        swimClass.CoachUserId = request.CoachUserId;
        swimClass.RegistrationStartAt = request.RegistrationStartAt.UtcDateTime;
        swimClass.RegistrationEndAt = request.RegistrationEndAt.UtcDateTime;
        swimClass.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new UpdateSwimClassResponse(
            swimClass.Id, swimClass.Name, swimClass.Description, swimClass.Capacity,
            swimClass.Fee, swimClass.CoachUserId, swimClass.RegistrationStartAt,
            swimClass.RegistrationEndAt, swimClass.Status, swimClass.CreatedByUserId,
            swimClass.CreatedAt, swimClass.UpdatedAt);
    }
}
