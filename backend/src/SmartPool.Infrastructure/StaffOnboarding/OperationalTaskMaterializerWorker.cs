using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Infrastructure.StaffOnboarding;

public sealed class OperationalTaskMaterializerWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OperationalTaskMaterializerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var operations = scope.ServiceProvider.GetRequiredService<IWaterQualityOperations>();
                await operations.MaterializeDueOperationalTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Operational task materialization failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
