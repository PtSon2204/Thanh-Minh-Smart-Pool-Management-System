using SmartPool.Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SmartPool.Infrastructure.Cameras;

namespace SmartPool.Infrastructure.SafetyMonitoring;

// Lấy khung JPEG mới nhất của camera để gộp vào luồng AI giám sát LIVE.
public sealed class CameraFrameSource(CameraService cameras, IOptions<CameraOptions> options, ILogger<CameraFrameSource>? logger = null, CameraDiagnostics? diagnostics = null) : ICameraFrameSource, IAsyncDisposable
{
    private readonly Dictionary<Guid, CameraAnalysisFramePump> pumps = new();
    private readonly Dictionary<Guid, string?> sessions = new();
    private readonly List<Task> retiring = new();
    private readonly Dictionary<Guid, DateTimeOffset> retryAt = new();
    private readonly object gate = new();
    private bool disposed;
    public CameraCapturedFrame? Capture(Guid cameraId)
    {
        // Các AI worker gọi đồng thời: tạo/xóa pump và duyệt dictionary phải cùng một lock.
        lock (gate) return disposed ? null : CaptureCore(cameraId);
    }

    private CameraCapturedFrame? CaptureCore(Guid cameraId)
    {
        // Retire asynchronously; never wait for FFmpeg shutdown while holding the gate.
        foreach (var id in pumps.Where(x => x.Value.Failed || cameras.GetStatus(x.Key).State != "Connected" ||
            cameras.GetStatus(x.Key).AnalysisSession != sessions.GetValueOrDefault(x.Key)).Select(x => x.Key).ToArray())
        {
            if (pumps[id].Failed && pumps[id].Failure != null)
            {
                logger?.LogWarning("Camera {CameraId} decoder restarted ({FailureCode}).", id, pumps[id].Failure);
                diagnostics?.Record(new CameraDiagnosticEvent(DateTimeOffset.UtcNow, "decoder_restart", id,
                    CameraSession: cameras.GetStatus(id).AnalysisSession, RestartReason: pumps[id].Failure));
            }
            retryAt[id] = DateTimeOffset.UtcNow.AddMilliseconds(cameras.GetStatus(id).AnalysisSession != null ? 100 : 1000);
            retiring.Add(pumps[id].DisposeAsync().AsTask());
            pumps.Remove(id);
            sessions.Remove(id);
        }
        retiring.RemoveAll(x => x.IsCompletedSuccessfully);
        var status = cameras.GetStatus(cameraId);
        if (status.State != "Connected") return null;
        if (retryAt.TryGetValue(cameraId, out var retry) && DateTimeOffset.UtcNow < retry) return null;
        if (!pumps.TryGetValue(cameraId, out var pump))
        {
            pumps[cameraId] = pump = new CameraAnalysisFramePump(cameras, cameraId, options.Value);
            sessions[cameraId] = status.AnalysisSession;
        }
        var frame = pump.Latest;
        return frame is not null && DateTimeOffset.UtcNow - frame.CapturedAt < TimeSpan.FromSeconds(2) ? frame : null;
    }

    public async ValueTask DisposeAsync()
    {
        Task[] pending;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            pending = pumps.Values.Select(x => x.DisposeAsync().AsTask()).Concat(retiring).ToArray();
            pumps.Clear(); sessions.Clear(); retryAt.Clear(); retiring.Clear();
        }
        await Task.WhenAll(pending);
    }
}
