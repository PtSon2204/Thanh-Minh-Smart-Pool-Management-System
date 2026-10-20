namespace SmartPool.Application.Interfaces.Services;

public sealed class CameraConnectionConfiguration
{
    public string Protocol { get; set; } = "RTSP";
    public string StreamUrl { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed record CameraConnectionStatus(string State, DateTimeOffset? LastFrameAt);

public interface ICameraRegistration : IAsyncDisposable
{
    string Identity { get; }
    string ProtectedConfiguration { get; }
    void Publish(Guid equipmentId);
    void Confirm();
}

public interface ICameraService
{
    Task<string> GetIdentityAsync(CameraConnectionConfiguration configuration, CancellationToken cancellationToken);
    Task<ICameraRegistration> PrepareAsync(CameraConnectionConfiguration configuration, CancellationToken cancellationToken);
    CameraConnectionStatus GetStatus(Guid equipmentId);
    byte[]? GetFrame(Guid equipmentId);
}

public sealed class CameraConnectionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
