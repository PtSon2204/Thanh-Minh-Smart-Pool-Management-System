using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Services;

namespace SmartPool.IntegrationTests.ManageStaffs;

public sealed class CompanyEmailGeneratorIntegrationTests
{
    [Fact]
    public async Task AttemptedCandidatesAreNeverReturnedAndExhaustionIsControlled()
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        await using var context = new SmartPoolDbContext(options);
        var generator = new CompanyEmailGenerator(context);
        var attempted = Enumerable.Range(0, 999).Select(i => $"suffixexamples{i:D3}@smartpool.com")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = await generator.GenerateAsync("Synthetic Suffixexample", attempted, default);
        Assert.Equal("suffixexamples999@smartpool.com", candidate);
        attempted.Add(candidate);
        var error = await Assert.ThrowsAsync<StaffOnboardingException>(() =>
            generator.GenerateAsync("Synthetic Suffixexample", attempted, default));
        Assert.Equal("COMPANY_EMAIL_UNAVAILABLE", error.Code);
        Assert.Equal(409, error.Status);
    }
}
