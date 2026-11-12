namespace SmartPool.Infrastructure.SafetyMonitoring;

// Local wake-up only. PostgreSQL remains the durable, multi-instance queue.
public sealed class VideoAnalysisQueue : IDisposable
{
    private readonly SemaphoreSlim signal = new(0, 1);
    private volatile bool workerReady;
    public bool WorkerReady => workerReady;
    public void SetWorkerReady(bool ready) => workerReady = ready;
    public void Notify()
    {
        try { signal.Release(); }
        catch (SemaphoreFullException) { }
    }
    public Task<bool> WaitAsync(CancellationToken ct) => signal.WaitAsync(TimeSpan.FromSeconds(2), ct);
    public void Dispose() => signal.Dispose();
}
