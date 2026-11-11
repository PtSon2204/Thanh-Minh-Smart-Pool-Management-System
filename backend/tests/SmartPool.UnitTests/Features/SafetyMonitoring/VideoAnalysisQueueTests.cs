using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class VideoAnalysisQueueTests
{
    [Fact]
    public async Task Notify_WakesWaitingWorker_AndCoalescesNotifications()
    {
        using var queue = new VideoAnalysisQueue();
        Assert.False(queue.WorkerReady);
        queue.SetWorkerReady(true);
        Assert.True(queue.WorkerReady);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waiting = queue.WaitAsync(deadline.Token);
        Assert.False(waiting.IsCompleted);
        queue.Notify();
        Assert.True(await waiting);
        queue.Notify(); queue.Notify();
        Assert.True(await queue.WaitAsync(deadline.Token));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.WaitAsync(cancelled.Token));
    }
}
