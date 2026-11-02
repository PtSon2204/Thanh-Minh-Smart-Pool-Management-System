using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.StaffOnboarding;

public sealed class StaffDocumentCleanup(SmartPoolDbContext context, IStaffDocumentStorage storage, TimeProvider clock)
{
    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await context.StaffOnboardingDocuments.AsNoTracking().Where(item => item.DeletionState != "DELETED" &&
            (item.DeletionState == "PENDING" || (item.Purpose != "AVATAR" && item.DeleteAfterUtc <= now) ||
             (item.DeletionState == "UPLOADING" && item.UploadedAtUtc <= now.AddMinutes(-10))) &&
            (item.RetryAtUtc == null || item.RetryAtUtc <= now) && (item.LeaseUntilUtc == null || item.LeaseUntilUtc <= now))
            .OrderBy(item => item.UploadedAtUtc).Select(item => item.Id).Take(20).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            var lease = Guid.NewGuid();
            var claimed = await context.StaffOnboardingDocuments.Where(item => item.Id == id && item.DeletionState != "DELETED" &&
                    (item.LeaseUntilUtc == null || item.LeaseUntilUtc <= now))
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.LeaseId, lease)
                    .SetProperty(item => item.LeaseUntilUtc, now.AddMinutes(2)).SetProperty(item => item.DeletionState, "PENDING"), cancellationToken);
            if (claimed == 0) continue;
            var document = await context.StaffOnboardingDocuments.AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken);
            var deleted = false;
            try { await storage.DeleteAsync(document, cancellationToken); deleted = true; }
            catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
            var attempts = document.AttemptCount + 1;
            var query = context.StaffOnboardingDocuments.Where(item => item.Id == id && item.LeaseId == lease);
            if (deleted && document.UploadSettledAtUtc.HasValue)
                await query.ExecuteUpdateAsync(update => update.SetProperty(item => item.DeletionState, "DELETED")
                    .SetProperty(item => item.DeletedAtUtc, now).SetProperty(item => item.PublicId, (string?)null)
                    .SetProperty(item => item.AssetId, (string?)null).SetProperty(item => item.AssetVersion, (string?)null)
                    .SetProperty(item => item.LastErrorCode, (string?)null).SetProperty(item => item.LeaseId, (Guid?)null)
                    .SetProperty(item => item.LeaseUntilUtc, (DateTime?)null).SetProperty(item => item.AttemptCount, attempts), cancellationToken);
            else
                await query.ExecuteUpdateAsync(update => update.SetProperty(item => item.LastErrorCode,
                        document.UploadSettledAtUtc.HasValue ? "PROVIDER_DELETE_UNCONFIRMED" : "UPLOAD_OUTCOME_UNCONFIRMED")
                    .SetProperty(item => item.RetryAtUtc, now.AddMinutes(Math.Min(60, attempts * 5)))
                    .SetProperty(item => item.AttemptCount, attempts).SetProperty(item => item.LeaseId, (Guid?)null)
                    .SetProperty(item => item.LeaseUntilUtc, (DateTime?)null), cancellationToken);
        }
    }
}

public sealed class StaffDocumentCleanupWorker(IServiceScopeFactory scopes, ILogger<StaffDocumentCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<StaffDocumentCleanup>().RunBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("Staff document cleanup did not complete; pending work remains durable."); }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
