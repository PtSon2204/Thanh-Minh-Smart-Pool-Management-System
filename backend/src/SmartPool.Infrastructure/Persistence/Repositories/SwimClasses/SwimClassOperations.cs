using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed class SwimClassOperations(SmartPoolDbContext context, TimeProvider timeProvider)
    : ISwimClassOperations
{
    public async Task<CreateSwimClassResponse> CreateAsync(
        CreateSwimClassCommand request,
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        var eligibleCoach = await context.Employees.AsNoTracking().AnyAsync(employee =>
            employee.UserId == request.CoachUserId &&
            employee.Status != null && EF.Functions.ILike(employee.Status, "Working") &&
            employee.User.IsDeleted != true &&
            employee.User.Status != null && EF.Functions.ILike(employee.User.Status, "ACTIVE") &&
            employee.User.Role != null && employee.User.Role.IsDeleted != true &&
            EF.Functions.ILike(employee.User.Role.Name, RoleEnum.COACH.ToString()),
            cancellationToken);

        if (!eligibleCoach)
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.CoachUserId),
                    "Coach phải là nhân viên đang làm việc, có tài khoản hoạt động và vai trò COACH.")
            });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var swimClass = new SwimClass
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Capacity = request.Capacity,
            Fee = request.Fee,
            CoachUserId = request.CoachUserId,
            RegistrationStartAt = request.RegistrationStartAt.UtcDateTime,
            RegistrationEndAt = request.RegistrationEndAt.UtcDateTime,
            Status = "Draft",
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.SwimClasses.Add(swimClass);
        await context.SaveChangesAsync(cancellationToken);

        return new CreateSwimClassResponse(
            swimClass.Id, swimClass.Name, swimClass.Description, swimClass.Capacity,
            swimClass.Fee, swimClass.CoachUserId, swimClass.RegistrationStartAt,
            swimClass.RegistrationEndAt, swimClass.Status, swimClass.CreatedByUserId,
            swimClass.CreatedAt, swimClass.UpdatedAt);
    }
}
