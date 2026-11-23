using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using Microsoft.Extensions.Logging;
using CloudinaryDotNet;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations(
    SmartPoolDbContext context,
    TimeProvider timeProvider,
    INotificationService notificationService,
    ILogger<SwimClassOperations> logger,
    Cloudinary cloudinary)
    : ISwimClassOperations
{
    public async Task<CreateSwimClassResponse> CreateAsync(
        CreateSwimClassCommand request,
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        var eligibleCoach = await EligibleCoaches()
            .AnyAsync(user => user.Id == createdByUserId, cancellationToken);

        if (!eligibleCoach)
            throw new ValidationException(new[]
            {
                new ValidationFailure("CreatedByUserId",
                    "Người tạo lớp phải có tài khoản hoạt động và là Coach.")
            });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var swimClass = new SwimClass
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Capacity = request.Capacity,
            Fee = request.Fee,
            CoachUserId = createdByUserId,
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

    private IQueryable<User> EligibleCoaches() => context.Users.AsNoTracking().Where(user =>
        user.IsDeleted != true &&
        user.Status != null && EF.Functions.ILike(user.Status, "ACTIVE") &&
        user.Role != null && user.Role.IsDeleted != true &&
        user.IsCoach);

    private static void EnsureClassOwner(SwimClass swimClass, Guid coachUserId)
    {
        if (swimClass.CreatedByUserId != coachUserId)
            throw new UnauthorizedAccessException("Chỉ Coach tạo lớp được quản lý lớp này.");
    }
}
