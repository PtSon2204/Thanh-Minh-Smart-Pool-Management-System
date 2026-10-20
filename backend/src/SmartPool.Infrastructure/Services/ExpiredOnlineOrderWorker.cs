using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Infrastructure.Services;

public sealed class ExpiredOnlineOrderWorker(IServiceScopeFactory scopeFactory,
    ILogger<ExpiredOnlineOrderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IOrderPaymentOperations>()
                    .ExpireDueOrdersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not expire online ticket orders; retrying next tick.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
