using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.StaffOnboarding;

public sealed class StaffScheduleDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<StaffScheduleDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning(exception, "Schedule notification delivery batch failed."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await context.StaffScheduleDeliveries.Where(item => item.Status == "Sending" && item.LeaseUntilUtc <= now)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Unknown")
                .SetProperty(item => item.LastErrorCode, "DELIVERY_RESULT_UNKNOWN")
                .SetProperty(item => item.LeaseId, (Guid?)null).SetProperty(item => item.LeaseUntilUtc, (DateTime?)null), cancellationToken);

        var leaseId = Guid.NewGuid();
        List<Domain.Entities.StaffScheduleDelivery> deliveries;
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            deliveries = await context.StaffScheduleDeliveries.FromSqlInterpolated($"""
                SELECT * FROM smart_pool.staff_schedule_deliveries
                WHERE "Status" = 'Pending'
                   OR ("Status" = 'Failed' AND "AttemptCount" < 3 AND
                       ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {now}))
                ORDER BY "CreatedAtUtc", "Id"
                LIMIT 20 FOR UPDATE SKIP LOCKED
                """).ToListAsync(cancellationToken);
            foreach (var delivery in deliveries)
            {
                delivery.Status = "Sending";
                delivery.AttemptCount++;
                delivery.LeaseId = leaseId;
                delivery.LeaseUntilUtc = now.AddMinutes(2);
                delivery.NextAttemptAtUtc = null;
            }
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        foreach (var delivery in deliveries)
        {
            var attemptTime = timeProvider.GetUtcNow().UtcDateTime;
            var ownsLease = await context.StaffScheduleDeliveries
                .Where(item => item.Id == delivery.Id && item.Status == "Sending" && item.LeaseId == leaseId &&
                    item.LeaseUntilUtc > attemptTime)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.LeaseUntilUtc, attemptTime.AddMinutes(2)), cancellationToken);
            if (ownsLease == 0) continue;

            var status = "Sent";
            string? errorCode = null;
            DateTime? nextAttemptAtUtc = null;
            var forceTerminalAttempt = false;
            if (!MailAddress.TryCreate(delivery.RecipientEmail, out var address) || address.Address != delivery.RecipientEmail)
            {
                status = "Failed";
                forceTerminalAttempt = true;
                errorCode = "RECIPIENT_EMAIL_INVALID";
            }
            else
            {
                try
                {
                    await email.SendAsync(delivery.RecipientEmail, delivery.Subject, delivery.HtmlBody, cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    status = "Failed";
                    errorCode = "EMAIL_DELIVERY_FAILED";
                    if (delivery.AttemptCount < 3) nextAttemptAtUtc = attemptTime.AddMinutes(Math.Pow(2, delivery.AttemptCount));
                    logger.LogWarning(exception, "Schedule email delivery {DeliveryId} failed on attempt {Attempt}.", delivery.Id, delivery.AttemptCount);
                }
            }
            await context.StaffScheduleDeliveries
                .Where(item => item.Id == delivery.Id && item.Status == "Sending" && item.LeaseId == leaseId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Status, status)
                    .SetProperty(item => item.AttemptCount, forceTerminalAttempt ? 3 : delivery.AttemptCount)
                    .SetProperty(item => item.LastErrorCode, errorCode)
                    .SetProperty(item => item.NextAttemptAtUtc, nextAttemptAtUtc)
                    .SetProperty(item => item.LeaseId, (Guid?)null)
                    .SetProperty(item => item.LeaseUntilUtc, (DateTime?)null), cancellationToken);
        }
    }
}
