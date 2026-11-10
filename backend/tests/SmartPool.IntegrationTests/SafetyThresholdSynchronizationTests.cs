using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    [Fact]
    public async Task Thresholds_TwoInstances_SerializeSavesAndRefreshSharedDatabaseVersion()
    {
        using var services = new ServiceCollection().AddScoped(_ => new SmartPoolDbContext(options)).BuildServiceProvider();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var first = new RiskThresholds(scopes, Options.Create(new RiskOptions()), NullLogger<RiskThresholds>.Instance);
        var second = new RiskThresholds(scopes, Options.Create(new RiskOptions()), NullLogger<RiskThresholds>.Instance);
        var initial = await first.SaveAsync(new RiskOptions { ConfidenceThreshold = .7 }.ToDto(0, "DATABASE"), userId, default);
        await second.InitializeAsync(default);
        Assert.Equal(.7, second.Current.ConfidenceThreshold);
        var saved = await Task.WhenAll(
            first.SaveAsync(new RiskOptions { ConfidenceThreshold = .8 }.ToDto(0, "DATABASE"), userId, default),
            second.SaveAsync(new RiskOptions { ConfidenceThreshold = .9 }.ToDto(0, "DATABASE"), userId, default));
        Assert.Equal(new[] { initial.Version + 1, initial.Version + 2 }, saved.Select(x => x.Version).Order().ToArray());
        await first.InitializeAsync(default);
        await second.InitializeAsync(default);
        var latest = saved.MaxBy(x => x.Version)!;
        Assert.Equal(latest.ConfidenceThreshold, first.Current.ConfidenceThreshold);
        Assert.Equal(latest.ConfidenceThreshold, second.Current.ConfidenceThreshold);
    }
}
