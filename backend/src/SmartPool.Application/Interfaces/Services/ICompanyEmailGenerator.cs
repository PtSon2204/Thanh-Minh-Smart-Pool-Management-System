using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.Application.Interfaces.Services;

public interface ICompanyEmailGenerator
{
    // Returns a free company sign-in identifier, e.g. lamnp231@smartpool.com.
    // Throws StaffOnboardingException when no suffix is free.
    Task<string> GenerateAsync(string fullName, IReadOnlySet<string> attempted, CancellationToken cancellationToken);
}
