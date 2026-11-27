using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.StaffOnboarding;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    private StaffOnboardingServices Onboarding => onboardingServices ?? throw new StaffOnboardingException("ONBOARDING_UNAVAILABLE", 503);
    private DateTime OnboardingNow => timeProvider.GetUtcNow().UtcDateTime;

    private Task LockOnboardingAsync(CancellationToken cancellationToken) => context.Database.ExecuteSqlRawAsync(
        "SELECT pg_advisory_xact_lock(704318229012)", cancellationToken);

    private async Task RequireOnboardingAdminAsync(Guid actorId, CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(item => item.Id == actorId && item.IsDeleted != true &&
            item.Status != null && EF.Functions.ILike(item.Status, "ACTIVE") && item.Role != null &&
            item.Role.IsDeleted != true && EF.Functions.ILike(item.Role.Name, "ADMIN"), cancellationToken))
            throw new StaffOnboardingException("ADMIN_REQUIRED", 403);
    }

    private async Task CheckPhoneAsync(string phone, Guid? employeeId, CancellationToken cancellationToken)
    {
        var international = "+84" + phone[1..];
        if (await context.Users.IgnoreQueryFilters().AnyAsync(item => item.Id != employeeId && item.Phone != null &&
            (item.Phone.Trim() == phone || item.Phone.Trim() == international), cancellationToken))
            throw new StaffOnboardingException("PHONE_ALREADY_EXISTS");
    }

}
