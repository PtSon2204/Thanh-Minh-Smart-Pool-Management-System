using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Channels;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.Infrastructure.Cameras;

// Boxes are rendered on their original pixels BEFORE encoding. There is no guessed overlay/RTP alignment.
// One bounded source per camera is shared by all AI viewers and never opens another RTSP connection.
internal sealed class CameraAnalysisVideoSource : IWebRtcVideoSource
{
    private readonly CameraAnalysisState analysis;
    private readonly Guid id;
    private readonly CancellationTokenSource stop = new();
    private readonly Channel<(DateTimeOffset Time, byte[] Jpeg)> input = Channel.CreateBounded<(DateTimeOffset, byte[])>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Task run;
    public bool Failed => run.IsCompleted;
    public bool Transcoding => true;
    public event Action<byte[], bool, uint>? Frame;

    public CameraAnalysisVideoSource(CameraAnalysisState analysis, Guid id, CameraOptions options)
    {
        this.analysis = analysis; this.id = id;
        analysis.FrameReady += Offer;
        run = Task.Run(() => RunAsync(options));
        var metadata = analysis.Get(id);
        var jpeg = metadata.FrameId == null ? null : analysis.Frame(id, metadata.FrameId);
        if (jpeg != null) Offer(id, metadata.ObservedAt!.Value, jpeg);
    }
    private void Offer(Guid cameraId, DateTimeOffset time, byte[] jpeg)
    {
        if (cameraId == id && !stop.IsCancellationRequested) input.Writer.TryWrite((time, jpeg));
    }
    private async Task RunAsync(CameraOptions options)
    {
        DateTimeOffset? previous = null;
        Process? process = null;
        Task reading = Task.CompletedTask, errors = Task.CompletedTask;
        var pending = new ConcurrentQueue<DateTimeOffset>();
        try
        {
            var start = new ProcessStartInfo(options.FfmpegPath) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-protocol_whitelist", "pipe",
                "-analyzeduration", "1", "-probesize", "32", "-fpsprobesize", "0", "-threads", "1",
                "-f", "image2pipe", "-vcodec", "mjpeg", "-framerate", "25", "-i", "pipe:0", "-an",
                "-vf", "scale=w='min(1920,iw)':h='min(1080,ih)':force_original_aspect_ratio=decrease:force_divisible_by=2:flags=lanczos", "-fps_mode", "passthrough",
                "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency", "-profile:v", "baseline", "-pix_fmt", "yuv420p",
                "-threads", "1", "-bf", "0", "-crf", "18", "-x264-params", "aud=1:repeat-headers=1:keyint=1",
                "-flush_packets", "1", "-f", "h264", "pipe:1" }) start.ArgumentList.Add(arg);
            process = Process.Start(start) ?? throw new IOException("AI video encoder unavailable.");
            errors = DrainAsync(process.StandardError.BaseStream, stop.Token);
            reading = H264AccessUnits.ReadAsync(process.StandardOutput.BaseStream, bytes =>
            {
                if (!pending.TryDequeue(out var time)) throw new IOException("AI video encoder lost frame alignment.");
                var duration = previous.HasValue ? (uint)Math.Clamp((time - previous.Value).TotalSeconds * 90000, 750, 450000) : 3600u;
                previous = time;
                Frame?.Invoke(bytes, true, duration);
            }, stop.Token);
            await foreach (var frame in input.Reader.ReadAllAsync(stop.Token))
            {
                if (DateTimeOffset.UtcNow - frame.Time > TimeSpan.FromSeconds(6)) continue;
                if (reading.IsCompleted || process.HasExited || pending.Count >= 32) throw new IOException("AI video encoder unavailable.");
                pending.Enqueue(frame.Time);
                await process.StandardInput.BaseStream.WriteAsync(frame.Jpeg, stop.Token);
                await process.StandardInput.BaseStream.FlushAsync(stop.Token);
            }
        }
        catch (Exception) { /* Never log frame bytes, model data or encoder diagnostics. */ }
        finally
        {
            analysis.FrameReady -= Offer;
            stop.Cancel();
            if (process != null)
            {
                try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); await Task.WhenAll(reading, errors).WaitAsync(TimeSpan.FromSeconds(2)); } catch (Exception) { }
                process.Dispose();
            }
        }
    }
    private static async Task DrainAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[4096];
        while (await stream.ReadAsync(buffer, token) > 0) { }
    }
    public async ValueTask DisposeAsync()
    {
        analysis.FrameReady -= Offer; stop.Cancel(); input.Writer.TryComplete();
        try { await run.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
    }
}
