using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SmartPool.Infrastructure.Cameras;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class CameraDiagnosticsOptions
{
    public bool Enabled { get; set; }
    public int MaxEventsPerCamera { get; set; } = 512;
    public int FrameSampleMilliseconds { get; set; } = 250;
}

public sealed record CameraDiagnosticEvent(
    DateTimeOffset At,
    string Kind,
    Guid CameraId,
    string? FrameId = null,
    uint? RtpTimestamp = null,
    DateTimeOffset? CapturedAt = null,
    double? InferenceMilliseconds = null,
    int DetectionCount = 0,
    IReadOnlyList<DiagnosticDetection>? Detections = null,
    IReadOnlyList<string>? ActiveRiskTrackIds = null,
    IReadOnlyList<DiagnosticBox>? RenderedRiskBoxes = null,
    IReadOnlyList<DiagnosticBox>? CurrentRiskBoxes = null,
    int RiskDecisionCount = 0,
    int DisplayRiskCount = 0,
    string? CameraSession = null,
    string? WorkerSession = null,
    string? RestartReason = null,
    double? RenderMilliseconds = null,
    double? AnalysisMilliseconds = null,
    double? ElapsedMilliseconds = null,
    string? FailureReason = null,
    string? ConnectionState = null,
    int? SubscriberCount = null);

public sealed record DiagnosticDetection(string TrackId, string Class, double Confidence, double[] Box);
public sealed record DiagnosticBox(string TrackId, string Episode, string Severity, double[] Box);

public sealed class CameraDiagnostics(IOptions<CameraOptions> cameraOptions)
{
    private readonly ConcurrentDictionary<Guid, Queue<CameraDiagnosticEvent>> events = new();
    private readonly ConcurrentDictionary<Guid, long> lastFrameAt = new();
    private readonly ConcurrentDictionary<Guid, (int Successes, int Failures)> broadcastCounts = new();

    public bool Enabled => cameraOptions.Value.Diagnostics.Enabled;

    public bool ShouldSampleFrame(Guid cameraId, DateTimeOffset now)
    {
        if (!Enabled) return false;
        var interval = Math.Clamp(cameraOptions.Value.Diagnostics.FrameSampleMilliseconds, 50, 60_000);
        var current = now.ToUnixTimeMilliseconds();
        while (true)
        {
            var previous = lastFrameAt.GetOrAdd(cameraId, 0);
            if (current - previous < interval) return false;
            if (lastFrameAt.TryUpdate(cameraId, current, previous)) return true;
        }
    }

    public void Record(CameraDiagnosticEvent item)
    {
        if (!Enabled) return;
        if (item.Kind == "broadcast_success")
            broadcastCounts.AddOrUpdate(item.CameraId, (1, 0), (_, value) => (value.Successes + 1, value.Failures));
        else if (item.Kind == "broadcast_failure")
            broadcastCounts.AddOrUpdate(item.CameraId, (0, 1), (_, value) => (value.Successes, value.Failures + 1));
        var queue = events.GetOrAdd(item.CameraId, _ => new Queue<CameraDiagnosticEvent>());
        lock (queue)
        {
            queue.Enqueue(item);
            var limit = Math.Clamp(cameraOptions.Value.Diagnostics.MaxEventsPerCamera, 16, 4096);
            while (queue.Count > limit) queue.Dequeue();
        }
    }

    public IReadOnlyList<CameraDiagnosticEvent> Get(Guid cameraId, int limit = 512)
    {
        if (!Enabled || !events.TryGetValue(cameraId, out var queue)) return [];
        lock (queue) return queue.TakeLast(Math.Clamp(limit, 1, 4096)).ToArray();
    }

    public void Clear(Guid cameraId)
    {
        events.TryRemove(cameraId, out _);
        broadcastCounts.TryRemove(cameraId, out _);
        lastFrameAt.TryRemove(cameraId, out _);
    }

    /// <summary>
    /// Đếm số sự kiện broadcast thành công/thất bại của một camera (cho báo cáo,
    /// không phụ thuộc giới hạn ring buffer).
    /// </summary>
    public (int Successes, int Failures) BroadcastCount(Guid cameraId) =>
        broadcastCounts.TryGetValue(cameraId, out var count) ? count : (0, 0);
}
