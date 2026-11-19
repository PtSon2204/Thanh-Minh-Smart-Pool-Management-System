using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartPool.Infrastructure.Services;

public sealed class StaffContractLifecycleWorker(IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider, ILogger<StaffContractLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<StaffContractLifecycle>().SynchronizeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning(exception, "Staff contract synchronization failed."); }
            await Task.Delay(TimeSpan.FromMinutes(1), timeProvider, stoppingToken);
        }
    }
}
