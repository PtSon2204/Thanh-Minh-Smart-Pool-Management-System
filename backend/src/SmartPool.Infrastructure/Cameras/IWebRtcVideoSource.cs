namespace SmartPool.Infrastructure.Cameras;

internal interface IWebRtcVideoSource : IAsyncDisposable
{
    bool Failed { get; }
    bool Transcoding { get; }
    event Action<byte[], bool, uint>? Frame;
}
