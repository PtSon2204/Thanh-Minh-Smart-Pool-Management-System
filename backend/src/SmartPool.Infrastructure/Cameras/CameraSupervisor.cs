using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraSupervisor(IServiceScopeFactory scopes, CameraService cameras, IOptions<CameraOptions> options, ILogger<CameraSupervisor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.AllowedEndpoints.Count == 0) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
                var equipment = await db.Equipments.AsNoTracking()
                    .Where(x => x.IsDeleted != true && x.Status == "Active" && x.Category.ToLower() == "camera")
                    .Select(x => new { x.Id, x.Metadata }).ToListAsync(stoppingToken);
                await cameras.ReconcileAsync(equipment.Select(x => x.Id).ToHashSet());
                foreach (var item in equipment)
                {
                    if (!CameraMetadata.TryGetProtectedConfiguration(item.Metadata, out var configuration)) continue;
                    try { await cameras.RestoreAsync(item.Id, configuration, stoppingToken); }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("Unable to restore camera {EquipmentId}; check camera configuration and encryption keys.", item.Id); }
                }
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("Camera registry could not be synchronized."); }
            try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (OperationCanceledException) { }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await cameras.DisposeAsync();
    }
}
