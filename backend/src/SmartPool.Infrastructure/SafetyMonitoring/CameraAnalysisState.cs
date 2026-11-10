using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class CameraAnalysisState(ICameraService cameras, IOptions<MonitoringOptions> options) : ICameraAnalysisReader
{
    private sealed record Entry(CameraAnalysisDto Metadata, byte[] Jpeg);
    private readonly ConcurrentDictionary<Guid, Entry> entries = new();
    private readonly ConcurrentDictionary<(Guid CameraId, string FrameId), Entry> frames = new();
    public event Action<Guid, DateTimeOffset, byte[]>? FrameReady;
    public void Set(Guid id, string frameId, DateTimeOffset observed, IReadOnlyList<RiskDecision> risks, byte[] jpeg, IReadOnlyList<TrackedDetection>? detections = null,
        double? inferenceMilliseconds = null)
    {
        entries[id] = new(new CameraAnalysisDto("Connected", frameId, observed, DateTimeOffset.UtcNow, risks, Detections: detections)
            { CameraId = id, InferenceMilliseconds = inferenceMilliseconds }, jpeg);
        frames[(id, frameId)] = entries[id];
        // Giữ một số frame gần nhất để GET metadata rồi GET JPEG không đua với suy luận kế tiếp.
        foreach (var old in frames.Where(x => x.Key.CameraId == id).OrderByDescending(x => x.Value.Metadata.ObservedAt).Skip(8))
            frames.TryRemove(old.Key, out _);
        FrameReady?.Invoke(id, observed, jpeg);
    }
    public void Remove(Guid id)
    {
        entries.TryRemove(id, out _);
        foreach (var key in frames.Keys.Where(x => x.CameraId == id)) frames.TryRemove(key, out _);
    }
    public CameraAnalysisDto Get(Guid id)
    {
        if (!options.Value.Configured) return new("NotConfigured", null, null, null, []);
        if (cameras.GetStatus(id).State != "Connected") return new("CameraOffline", null, null, null, []);
        return entries.TryGetValue(id, out var entry) && DateTimeOffset.UtcNow - entry.Metadata.ObservedAt < TimeSpan.FromSeconds(6)
            ? entry.Metadata with { ServerTime = DateTimeOffset.UtcNow } : new("Offline", null, null, null, []);
    }
    public byte[]? Frame(Guid id, string frameId) => Get(id).State == "Connected" && frames.TryGetValue((id, frameId), out var entry)
        && DateTimeOffset.UtcNow - entry.Metadata.ObservedAt < TimeSpan.FromSeconds(6)
        ? entry.Jpeg : null;
}
