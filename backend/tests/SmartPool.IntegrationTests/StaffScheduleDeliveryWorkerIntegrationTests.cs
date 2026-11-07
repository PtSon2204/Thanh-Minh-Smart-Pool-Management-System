using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        var operationId = Guid.NewGuid();
        var deliveries = new[]
        {
            (Id: Guid.NewGuid(), RecipientId: fixture.EmployeeId, CreatedAtUtc: now),
            (Id: Guid.NewGuid(), RecipientId: fixture.AdminId, CreatedAtUtc: now.AddSeconds(1))
        };
        var firstDeliveryId = deliveries[0].Id;
        foreach (var delivery in deliveries)
            fixture.Context.StaffScheduleDeliveries.Add(new StaffScheduleDelivery
            {
                Id = delivery.Id,
                OperationId = operationId,
                EmployeeId = fixture.EmployeeId,
                RecipientId = delivery.RecipientId,
                Channel = "EMAIL",
                RecipientEmail = $"{delivery.Id:N}@example.test",
                Subject = "Synthetic delivery",
                HtmlBody = "Synthetic body",
                Status = "Pending",
                AttemptCount = 0,
                CreatedAtUtc = delivery.CreatedAtUtc
            });
        await fixture.Context.SaveChangesAsync();

        var options = new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql(fixture.Context.Database.GetConnectionString()).Options;
        var mail = new LeasedEmailService(options, firstDeliveryId, fixture.Clock.GetUtcNow().AddMinutes(3).UtcDateTime);
        var logger = new CapturingLogger();
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped(_ => new SmartPoolDbContext(options))
            .AddSingleton<IEmailService>(mail)
            .BuildServiceProvider();
        using var worker = new StaffScheduleDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(),
            fixture.Clock, logger);

        try
        {
            await worker.StartAsync(CancellationToken.None);
            await WaitForSignalOrWorkerFailureAsync(mail.FirstSendStarted.Task, logger, "first email send");
            var first = await fixture.Context.StaffScheduleDeliveries.AsNoTracking().SingleAsync(item => item.Id == firstDeliveryId);
            Assert.Equal("Unknown", first.Status);
            Assert.Equal("DELIVERY_RESULT_UNKNOWN", first.LastErrorCode);

            mail.ReleaseFirstSend.TrySetResult(true);
            await WaitForSignalOrWorkerFailureAsync(mail.SecondSendStarted.Task, logger, "second email send");

            var finalFirst = await fixture.Context.StaffScheduleDeliveries.AsNoTracking().SingleAsync(item => item.Id == firstDeliveryId);
            Assert.Equal("Unknown", finalFirst.Status);
            Assert.Equal("DELIVERY_RESULT_UNKNOWN", finalFirst.LastErrorCode);
        }
        finally
        {
            mail.ReleaseFirstSend.TrySetResult(true);
            using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await worker.StopAsync(shutdownTimeout.Token);
            }
            finally
            {
                await services.DisposeAsync();
            }
        }
    }

    private static async Task WaitForSignalOrWorkerFailureAsync(Task signal, CapturingLogger logger, string expectedAction)
    {
        var completed = await Task.WhenAny(signal, logger.BatchFailure.Task).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(ReferenceEquals(signal, completed),
            $"The schedule delivery worker failed before the {expectedAction}: {await logger.BatchFailure.Task}");
        await signal;
    }

    private sealed class CapturingLogger : ILogger<StaffScheduleDeliveryWorker>
    {
        public TaskCompletionSource<Exception> BatchFailure { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) BatchFailure.TrySetResult(exception);
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
