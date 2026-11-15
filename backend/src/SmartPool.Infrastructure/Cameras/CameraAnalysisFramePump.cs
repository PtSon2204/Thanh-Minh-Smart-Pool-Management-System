using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

// Decode the shared RTSP stream continuously. Inference reads only the newest JPEG;
// it never waits for the next keyframe or spawns FFmpeg once per observation.
internal sealed class CameraAnalysisFramePump : IAsyncDisposable
{
    private readonly Channel<(CameraEncodedFrame Frame, DateTimeOffset Time)> input =
        Channel.CreateBounded<(CameraEncodedFrame, DateTimeOffset)>(32);
    private readonly CancellationTokenSource stop = new();
    private readonly IDisposable subscription;
    private readonly Task run;
    private CameraCapturedFrame? latest;
    private long queuedBytes;
    public bool Failed => run.IsCompleted;
    public CameraCapturedFrame? Latest => Volatile.Read(ref latest);

    public CameraAnalysisFramePump(CameraService cameras, Guid id, CameraOptions options)
    {
        subscription = cameras.Subscribe(id, Offer);
        run = Task.Run(() => RunAsync(options));
    }

    private void Offer(CameraEncodedFrame frame)
    {
        if (stop.IsCancellationRequested) return;
        if (Interlocked.Add(ref queuedBytes, frame.Data.Length) > 4 * 1024 * 1024 ||
            !input.Writer.TryWrite((frame, DateTimeOffset.UtcNow)))
        {
            Interlocked.Add(ref queuedBytes, -frame.Data.Length);
            // Dropping arbitrary P frames corrupts decoding. Restart at a fresh keyframe instead.
            stop.Cancel();
        }
    }

    private async Task RunAsync(CameraOptions options)
    {
        Process? process = null;
        Task output = Task.CompletedTask, errors = Task.CompletedTask;
        var pending = new ConcurrentQueue<DateTimeOffset>();
        try
        {
            await foreach (var item in input.Reader.ReadAllAsync(stop.Token))
            {
                Interlocked.Add(ref queuedBytes, -item.Frame.Data.Length);
                if (process is null)
                {
                    if (!item.Frame.IsKeyFrame) continue;
                    var start = new ProcessStartInfo(options.FfmpegPath) { UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-protocol_whitelist", "pipe",
                        "-analyzeduration", "1", "-probesize", "32768", "-fpsprobesize", "0", "-threads", "1", "-flags", "low_delay",
                        "-f", item.Frame.Format, "-i", "pipe:0", "-an", "-vf", "scale=640:-2", "-fps_mode", "passthrough",
                        "-c:v", "mjpeg", "-q:v", "5", "-threads", "1", "-flush_packets", "1", "-f", "image2pipe", "pipe:1" })
                        start.ArgumentList.Add(arg);
                    process = Process.Start(start) ?? throw new IOException("Camera analysis decoder unavailable.");
                    errors = DrainAsync(process.StandardError.BaseStream, stop.Token);
                    output = ReadJpegsAsync(process.StandardOutput.BaseStream, jpeg =>
                    {
                        // Timestamps describe incoming frames, not the end of inference.
                        if (!pending.TryDequeue(out var observed)) throw new IOException("Camera decoder lost frame alignment.");
                        Volatile.Write(ref latest, new CameraCapturedFrame(jpeg, observed, Guid.NewGuid().ToString("N")));
                    }, stop.Token);
                }
                if (process.HasExited || output.IsCompleted || pending.Count >= 128) throw new IOException("Camera analysis decoder stalled.");
                pending.Enqueue(item.Time);
                await process.StandardInput.BaseStream.WriteAsync(item.Frame.Data, stop.Token);
                await process.StandardInput.BaseStream.FlushAsync(stop.Token);
            }
        }
        catch (Exception) { /* Never log encoded frames, RTSP credentials or decoder diagnostics. */ }
        finally
        {
            subscription.Dispose();
            stop.Cancel();
            Volatile.Write(ref latest, null);
            if (process is not null)
            {
                try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); await Task.WhenAll(output, errors).WaitAsync(TimeSpan.FromSeconds(2)); } catch (Exception) { }
                process.Dispose();
            }
        }
    }

    private static async Task ReadJpegsAsync(Stream stream, Action<byte[]> emit, CancellationToken ct)
    {
        var buffer = new byte[16384];
        using var frame = new MemoryStream();
        int previous = -1, count;
        bool started = false;
        while ((count = await stream.ReadAsync(buffer, ct)) > 0)
        {
            for (var i = 0; i < count; i++)
            {
                var value = buffer[i];
                if (!started && previous == 0xff && value == 0xd8)
                {
                    frame.WriteByte(0xff); started = true;
                }
                if (started)
                {
                    frame.WriteByte(value);
                    if (frame.Length > 4 * 1024 * 1024) throw new IOException("Decoded camera frame exceeds limit.");
                    if (previous == 0xff && value == 0xd9)
                    {
                        emit(frame.ToArray()); frame.SetLength(0); started = false;
                    }
                }
                previous = value;
            }
        }
    }

    private static async Task DrainAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (await stream.ReadAsync(buffer, ct) > 0) { }
    }

    public async ValueTask DisposeAsync()
    {
        subscription.Dispose(); stop.Cancel(); input.Writer.TryComplete();
        try { await run.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
    }
}
