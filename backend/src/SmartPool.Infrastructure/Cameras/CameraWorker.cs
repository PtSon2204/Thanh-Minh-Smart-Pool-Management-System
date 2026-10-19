using System.Net;
using System.Threading.Channels;
using SharpRTSPClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

internal sealed class CameraWorker : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly CancellationTokenSource stop = new();
    private readonly Task work;
    private RTSPClient? currentClient;
    private string state = "Connecting";
    private DateTimeOffset? lastFrameAt;
    private byte[]? jpeg;
    private readonly TimeProvider timeProvider;
    private readonly CameraOptions options;
    private event Action<CameraEncodedFrame>? liveFrames;

    internal IDisposable Subscribe(Action<CameraEncodedFrame> receive)
    {
        liveFrames += receive;
        return new Subscription(() => liveFrames -= receive);
    }
    public Guid? EquipmentId { get; set; }
    public bool Registered { get; set; }
    public TaskCompletionSource<CameraConnectionException?> FirstFrame { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CameraWorker(CameraConnectionConfiguration configuration, CameraTarget target, ICameraFrameDecoder decoder,
        CameraOptions options, TimeProvider timeProvider, ILogger logger)
    {
        this.timeProvider = timeProvider;
        this.options = options;
        work = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                var frames = Channel.CreateBounded<(byte[] Data, string Format)>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                Task receiving = Task.CompletedTask;
                Task decoding = Task.CompletedTask;
                // Library logs can contain authorization headers; use only our safe reason codes.
                using var client = new RTSPClient(NullLoggerFactory.Instance)
                {
                    AcceptTrack = track => track.Kind == TrackKind.Video && track.AcceptedSoFar == 0
                        && track.Codec.ToUpperInvariant() is "H264" or "H265" or "JPEG"
                };
                currentClient = client;
                var ended = new TaskCompletionSource<StoppedReason>(TaskCreationOptions.RunContinuationsAsynchronously);
                client.Stopped += (_, e) => ended.TrySetResult(e.Reason);
                var assembler = new CameraKeyFrame();
                client.NewTrack += (_, e) => { lock (assembler) assembler.Configure(e.Codec, e.StreamConfigurationData); };
                DateTimeOffset nextDecode = DateTimeOffset.MinValue;
                var receivedAt = timeProvider.GetUtcNow();
                client.ReceivedData += (_, frame) =>
                {
                    lock (assembler)
                    {
                        receivedAt = timeProvider.GetUtcNow();
                        var listeners = liveFrames;
                        var encoded = assembler.Read(frame.Data.Data, listeners != null || receivedAt >= nextDecode, keyFramesOnly: listeners == null);
                        if (encoded == null) return;
                        if (encoded.Value.IsKeyFrame && receivedAt >= nextDecode)
                        {
                            frames.Writer.TryWrite((encoded.Value.Data, encoded.Value.Format));
                            nextDecode = receivedAt.AddSeconds(2);
                        }
                        listeners?.Invoke(new CameraEncodedFrame(encoded.Value.Data, encoded.Value.Format, encoded.Value.IsKeyFrame, frame.Data.RtpTimestamp));
                    }
                };
                try
                {
                    SetState("Connecting");
                    // Connect is synchronous in this library. Bound the caller's wait and
                    // stop again if a cancelled TCP connect completes later.
                    var connecting = Task.Run(() =>
                    {
                        try { client.Connect(target.Uri, RTPTransport.TCP, new NetworkCredential(configuration.Username, configuration.Password)); }
                        catch (Exception) when (attempt.IsCancellationRequested) { }
                        finally { if (attempt.IsCancellationRequested) client.Stop(); }
                    });
                    receiving = connecting;
                    await connecting.WaitAsync(TimeSpan.FromSeconds(Math.Clamp(options.ConnectionTimeoutSeconds, 2, 60)), attempt.Token);
                    receiving = Task.Run(async () =>
                    {
                        while (!ended.Task.IsCompleted)
                        {
                            await Task.Delay(250, attempt.Token);
                            if (ended.Task.IsCompleted) break;
                            lock (assembler)
                            {
                                if (client.StreamingFinished() || timeProvider.GetUtcNow() - receivedAt > TimeSpan.FromSeconds(Math.Clamp(options.FrameStaleSeconds, 3, 60)))
                                    throw new IOException("Camera stream ended.");
                            }
                        }
                        if (await ended.Task == StoppedReason.Unauthorized) throw new CameraAuthenticationException();
                        throw new IOException("Camera stream ended.");
                    }, attempt.Token);
                    decoding = Task.Run(async () =>
                    {
                        await foreach (var encoded in frames.Reader.ReadAllAsync(attempt.Token))
                        {
                            var image = await decoder.DecodeAsync(encoded.Data, encoded.Format, attempt.Token);
                            if (image == null) continue;
                            lock (gate) { jpeg = image; lastFrameAt = timeProvider.GetUtcNow(); state = "Connected"; }
                            FirstFrame.TrySetResult(null);
                        }
                    }, attempt.Token);
                    await await Task.WhenAny(receiving, decoding);
                    throw new IOException("Camera stream ended.");
                }
                catch (Exception exception) when (!stop.IsCancellationRequested)
                {
                    var authenticationFailed = exception is CameraAuthenticationException;
                    var code = authenticationFailed ? "AuthenticationFailed" : "StreamUnavailable";
                    SetState(authenticationFailed ? "Error" : "Disconnected");
                    FirstFrame.TrySetResult(new CameraConnectionException(code, authenticationFailed
                        ? "Không thể xác thực với camera. Vui lòng kiểm tra username/password."
                        : "Không thể kết nối đến camera. Vui lòng kiểm tra cấu hình kết nối."));
                    // The library exception may contain a URI or credentials. Log only safe identifiers.
                    logger.LogWarning("Camera {EquipmentId} connection lost: {Reason}", EquipmentId, code);
                }
                catch (Exception) when (stop.IsCancellationRequested) { }
                finally
                {
                    attempt.Cancel();
                    client.Dispose();
                    currentClient = null;
                    frames.Writer.TryComplete();
                    try { await Task.WhenAll(receiving, decoding).WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) { }
                }
                if (!stop.IsCancellationRequested)
                    try { await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.ReconnectDelaySeconds, 1, 60)), stop.Token); } catch (OperationCanceledException) { }
            }
        });
    }

    public CameraConnectionStatus GetStatus()
    {
        lock (gate)
        {
            var current = state == "Connected" && (lastFrameAt == null || timeProvider.GetUtcNow() - lastFrameAt > TimeSpan.FromSeconds(Math.Clamp(options.FrameStaleSeconds, 3, 60)))
                ? "Disconnected" : state;
            return new CameraConnectionStatus(current, lastFrameAt);
        }
    }

    public byte[]? GetFrame() { lock (gate) return GetStatus().State == "Connected" ? jpeg : null; }
    private void SetState(string value) { lock (gate) { state = value; if (value != "Connected") jpeg = null; } }

    private sealed class CameraAuthenticationException : Exception;
    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? release = unsubscribe;
        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        currentClient?.Dispose();
        try { await work.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
        SetState("Disconnected");
    }
}

internal sealed record CameraEncodedFrame(byte[] Data, string Format, bool IsKeyFrame, uint RtpTimestamp);
