using SmartPool.Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using SmartPool.Infrastructure.Cameras;

namespace SmartPool.Infrastructure.SafetyMonitoring;

// Lấy khung JPEG mới nhất của camera để gộp vào luồng AI giám sát LIVE.
public sealed class CameraFrameSource(CameraService cameras, IOptions<CameraOptions> options) : ICameraFrameSource, IAsyncDisposable
{
    private readonly Dictionary<Guid, CameraAnalysisFramePump> pumps = new();
    private readonly List<Task> retiring = new();
    private readonly Dictionary<Guid, DateTimeOffset> retryAt = new();
    public CameraCapturedFrame? Capture(Guid cameraId)
    {
        // LiveMonitoringWorker is the single reader. Dispose removed/offline sources without
        // blocking inference; keep no unbounded queue of historical JPEG frames.
        foreach (var id in pumps.Where(x => x.Value.Failed || cameras.GetStatus(x.Key).State != "Connected").Select(x => x.Key).ToArray())
        {
            retryAt[id] = DateTimeOffset.UtcNow.AddSeconds(1);
            retiring.Add(pumps[id].DisposeAsync().AsTask());
            pumps.Remove(id);
        }
        retiring.RemoveAll(x => x.IsCompletedSuccessfully);
        var status = cameras.GetStatus(cameraId);
        if (status.State != "Connected") return null;
        if (retryAt.TryGetValue(cameraId, out var retry) && DateTimeOffset.UtcNow < retry) return null;
        if (!pumps.TryGetValue(cameraId, out var pump))
            pumps[cameraId] = pump = new CameraAnalysisFramePump(cameras, cameraId, options.Value);
        var frame = pump.Latest;
        return frame is not null && DateTimeOffset.UtcNow - frame.CapturedAt < TimeSpan.FromSeconds(2) ? frame : null;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(pumps.Values.Select(x => x.DisposeAsync().AsTask()).Concat(retiring));
        pumps.Clear();
    }
}
