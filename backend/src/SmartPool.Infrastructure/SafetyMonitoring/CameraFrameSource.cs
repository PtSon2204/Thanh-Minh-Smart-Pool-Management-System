using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.SafetyMonitoring;

// Lấy khung JPEG mới nhất của camera để gộp vào luồng AI giám sát LIVE.
public sealed class CameraFrameSource(ICameraService cameras) : ICameraFrameSource
{
    public CameraCapturedFrame? Capture(Guid cameraId)
    {
        var status = cameras.GetStatus(cameraId);
        if (status.State != "Connected") return null;
        var jpeg = cameras.GetFrame(cameraId);
        return jpeg is null ? null : new(jpeg, status.LastFrameAt ?? DateTimeOffset.UtcNow, Guid.NewGuid().ToString("N"));
    }
}
