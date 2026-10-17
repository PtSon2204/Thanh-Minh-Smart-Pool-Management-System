using System.Net;
using System.Threading.Channels;
using RtspClientSharp;
using RtspClientSharp.RawFrames.Video;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

internal sealed class CameraWorker : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly CancellationTokenSource stop = new();
    private readonly Task work;
    private RtspClient? currentClient;
    private string state = "Connecting";
    private DateTimeOffset? lastFrameAt;
    private byte[]? jpeg;
    private readonly TimeProvider timeProvider;
    private readonly CameraOptions options;
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
                var parameters = new ConnectionParameters(target.Uri, new NetworkCredential(configuration.Username, configuration.Password))
                {
                    RtpTransport = RtpTransportProtocol.TCP, RequiredTracks = RequiredTracks.Video,
                    ConnectTimeout = TimeSpan.FromSeconds(Math.Clamp(options.ConnectionTimeoutSeconds, 2, 60)),
                    ReceiveTimeout = TimeSpan.FromSeconds(Math.Clamp(options.FrameStaleSeconds, 3, 60)), CancelTimeout = TimeSpan.FromSeconds(1)
                };
                using var client = new RtspClient(parameters);
                currentClient = client;
                DateTimeOffset nextDecode = DateTimeOffset.MinValue;
                client.FrameReceived += (_, frame) =>
                {
                    if (frame is not (RawH264IFrame or RawJpegFrame) || timeProvider.GetUtcNow() < nextDecode) return;
                    var prefix = frame is RawH264IFrame keyFrame ? keyFrame.SpsPpsSegment : default;
                    if (frame.FrameSegment.Count + prefix.Count > 4 * 1024 * 1024) return;
                    var data = new byte[prefix.Count + frame.FrameSegment.Count];
                    if (prefix.Count > 0) Buffer.BlockCopy(prefix.Array!, prefix.Offset, data, 0, prefix.Count);
                    Buffer.BlockCopy(frame.FrameSegment.Array!, frame.FrameSegment.Offset, data, prefix.Count, frame.FrameSegment.Count);
                    frames.Writer.TryWrite((data, frame is RawJpegFrame ? "mjpeg" : "h264"));
                    nextDecode = timeProvider.GetUtcNow().AddSeconds(2);
                };
                try
                {
                    SetState("Connecting");
                    await client.ConnectAsync(attempt.Token);
                    receiving = client.ReceiveAsync(attempt.Token);
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
                    var authenticationFailed = exception.GetType().Name.Contains("Credential", StringComparison.OrdinalIgnoreCase);
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

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        currentClient?.Dispose();
        try { await work.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
        SetState("Disconnected");
    }
}
