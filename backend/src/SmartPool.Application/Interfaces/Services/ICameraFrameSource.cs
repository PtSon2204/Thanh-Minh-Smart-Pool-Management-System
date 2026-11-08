namespace SmartPool.Application.Interfaces.Services;

public sealed record CameraCapturedFrame(byte[] Jpeg, DateTimeOffset CapturedAt, string FrameId);
public interface ICameraFrameSource
{
    CameraCapturedFrame? Capture(Guid cameraId);
}
