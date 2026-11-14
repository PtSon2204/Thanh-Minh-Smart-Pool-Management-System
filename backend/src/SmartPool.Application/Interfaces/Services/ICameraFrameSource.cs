namespace SmartPool.Application.Interfaces.Services;

public sealed record CameraCapturedFrame(byte[] Jpeg, DateTimeOffset CapturedAt, string FrameId, uint? RtpTimestamp = null);
public interface ICameraFrameSource
{
    CameraCapturedFrame? Capture(Guid cameraId);
}
