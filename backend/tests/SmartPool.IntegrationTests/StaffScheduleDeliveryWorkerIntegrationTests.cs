using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.StaffOnboarding;

namespace SmartPool.IntegrationTests;

public sealed class StaffScheduleDeliveryWorkerIntegrationTests
{
    [Fact]
    public async Task ExpiredLeaseCannotBeOverwrittenByTheOriginalSendingWorker()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var firstDeliveryId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var secondDeliveryId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var operationId = Guid.NewGuid();
        foreach (var (id, recipientId) in new[] { (firstDeliveryId, fixture.EmployeeId), (secondDeliveryId, fixture.AdminId) })
            fixture.Context.StaffScheduleDeliveries.Add(new StaffScheduleDelivery
            {
                Id = id,
                OperationId = operationId,
                EmployeeId = fixture.EmployeeId,
                RecipientId = recipientId,
                Channel = "EMAIL",
                RecipientEmail = $"{id:N}@example.test",
                Subject = "Synthetic delivery",
                HtmlBody = "Synthetic body",
                Status = "Pending",
                AttemptCount = 0,
                CreatedAtUtc = now
            });
        await fixture.Context.SaveChangesAsync();

        var options = new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql(fixture.Context.Database.GetConnectionString()).Options;
        var mail = new LeasedEmailService(options, firstDeliveryId, fixture.Clock.GetUtcNow().AddMinutes(3).UtcDateTime);
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped(_ => new SmartPoolDbContext(options))
            .AddSingleton<IEmailService>(mail)
            .BuildServiceProvider();
        using var worker = new StaffScheduleDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(),
            fixture.Clock, NullLogger<StaffScheduleDeliveryWorker>.Instance);

        try
        {
            await worker.StartAsync(CancellationToken.None);
            await mail.FirstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var first = await fixture.Context.StaffScheduleDeliveries.AsNoTracking().SingleAsync(item => item.Id == firstDeliveryId);
            Assert.Equal("Unknown", first.Status);
            Assert.Equal("DELIVERY_RESULT_UNKNOWN", first.LastErrorCode);

            mail.ReleaseFirstSend.TrySetResult(true);
            await mail.SecondSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var finalFirst = await fixture.Context.StaffScheduleDeliveries.AsNoTracking().SingleAsync(item => item.Id == firstDeliveryId);
            Assert.Equal("Unknown", finalFirst.Status);
            Assert.Equal("DELIVERY_RESULT_UNKNOWN", finalFirst.LastErrorCode);
        }
        finally
        {
            mail.ReleaseFirstSend.TrySetResult(true);
            await worker.StopAsync(CancellationToken.None);
            await services.DisposeAsync();
        }
    }

    private sealed class LeasedEmailService(DbContextOptions<SmartPoolDbContext> options, Guid firstDeliveryId,
        DateTime expiredAtUtc) : IEmailService
    {
        private int calls;
        public TaskCompletionSource<bool> FirstSendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseFirstSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondSendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1)
            {
                await using var context = new SmartPoolDbContext(options);
                await context.StaffScheduleDeliveries.Where(item => item.Id == firstDeliveryId && item.Status == "Sending" &&
                    item.LeaseUntilUtc <= expiredAtUtc)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Unknown")
                        .SetProperty(item => item.LastErrorCode, "DELIVERY_RESULT_UNKNOWN")
                        .SetProperty(item => item.LeaseId, (Guid?)null)
                        .SetProperty(item => item.LeaseUntilUtc, (DateTime?)null), cancellationToken);
                FirstSendStarted.TrySetResult(true);
                await ReleaseFirstSend.Task.WaitAsync(cancellationToken);
                return;
            }

            SecondSendStarted.TrySetResult(true);
        }

        public Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
