using System.Diagnostics;
using System.Threading.Channels;
using SIPSorcery.Net;

namespace SmartPool.Infrastructure.Cameras;

// One source per camera. Browser peers share this source and the existing RTSP worker.
internal sealed class CameraWebRtcSource : IAsyncDisposable
{
    private readonly Channel<CameraEncodedFrame> input = Channel.CreateBounded<CameraEncodedFrame>(32);
    private readonly CancellationTokenSource stop = new();
    private readonly IDisposable subscription;
    private readonly Task run;
    private long queuedBytes;
    private bool? passthrough;
    public bool Failed => run.IsCompleted;
    public bool Transcoding => passthrough != true;
    public event Action<byte[], bool, uint>? Frame;

    public CameraWebRtcSource(CameraService cameras, Guid id, CameraOptions options)
    {
        subscription = cameras.Subscribe(id, Offer);
        run = Task.Run(() => RunAsync(options));
    }

    private void Offer(CameraEncodedFrame frame)
    {
        if (stop.IsCancellationRequested) return;
        if (Interlocked.Add(ref queuedBytes, frame.Data.Length) > 4 * 1024 * 1024 || !input.Writer.TryWrite(frame))
        {
            Interlocked.Add(ref queuedBytes, -frame.Data.Length);
            stop.Cancel(); // Resync with a fresh session instead of building a latency queue.
        }
    }

    private async Task RunAsync(CameraOptions options)
    {
        Process? encoder = null;
        Task output = Task.CompletedTask, errors = Task.CompletedTask;
        uint? previousTimestamp = null;
        try
        {
            await foreach (var frame in input.Reader.ReadAllAsync(stop.Token))
            {
                Interlocked.Add(ref queuedBytes, -frame.Data.Length);
                if (passthrough == null)
                {
                    if (!frame.IsKeyFrame) continue;
                    passthrough = frame.Format == "h264" && H264Packetiser.ParseNals(frame.Data).Any(n => n.NAL.Length >= 4
                        && (n.NAL[0] & 31) == 7 && n.NAL[1] == 66 && (n.NAL[2] & 0x40) != 0 && n.NAL[3] <= 31);
                    if (!passthrough.Value)
                    {
                        var start = new ProcessStartInfo(options.FfmpegPath)
                        {
                            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                            RedirectStandardOutput = true, RedirectStandardError = true
                        };
                        foreach (var arg in new[] {
                            "-hide_banner", "-loglevel", "error", "-nostdin", "-protocol_whitelist", "pipe",
                            "-use_wallclock_as_timestamps", "1", "-analyzeduration", "1", "-probesize", "32768", "-fpsprobesize", "0",
                            "-threads", "1", "-flags", "low_delay",
                            "-f", frame.Format, "-i", "pipe:0", "-an", "-vf", "scale=w='min(1280,iw)':h=-2,fps=15",
                            "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency", "-profile:v", "baseline", "-level:v", "3.1",
                            "-pix_fmt", "yuv420p", "-threads", "2", "-bf", "0", "-b:v", "1500k", "-maxrate", "2000k", "-bufsize", "1000k",
                            "-x264-params", "aud=1:repeat-headers=1:keyint=15:min-keyint=15:scenecut=0",
                            "-flush_packets", "1", "-f", "h264", "pipe:1"
                        }) start.ArgumentList.Add(arg);
                        encoder = Process.Start(start) ?? throw new IOException("Video encoder unavailable.");
                        output = H264AccessUnits.ReadAsync(encoder.StandardOutput.BaseStream,
                            bytes => Frame?.Invoke(bytes, H264Packetiser.ParseNals(bytes).Any(n => (n.NAL[0] & 31) == 5), 6000), stop.Token);
                        errors = DrainAsync(encoder.StandardError.BaseStream, stop.Token);
                    }
                }
                if (passthrough == true)
                {
                    var duration = previousTimestamp.HasValue ? unchecked(frame.RtpTimestamp - previousTimestamp.Value) : 3600u;
                    previousTimestamp = frame.RtpTimestamp;
                    Frame?.Invoke(frame.Data, frame.IsKeyFrame, Math.Clamp(duration, 750u, 180000u));
                }
                else
                {
                    if (output.IsCompleted || encoder!.HasExited) throw new IOException("Video encoder ended.");
                    await encoder.StandardInput.BaseStream.WriteAsync(frame.Data, stop.Token);
                    await encoder.StandardInput.BaseStream.FlushAsync(stop.Token);
                }
            }
        }
        catch (Exception) { /* Encoded data and FFmpeg diagnostics never enter logs. */ }
        finally
        {
            subscription.Dispose();
            stop.Cancel();
            if (encoder != null)
            {
                try { if (!encoder.HasExited) encoder.Kill(true); } catch (InvalidOperationException) { }
                try { await encoder.WaitForExitAsync(); await Task.WhenAll(output, errors); } catch (Exception) { }
                encoder.Dispose();
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
        subscription.Dispose();
        stop.Cancel();
        input.Writer.TryComplete();
        try { await run.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
    }
}

// FFmpeg emits AUD-delimited Annex B access units. Keep at most one bounded frame,
// and emit when the next AUD arrives (one frame of delay, no HLS segment buffer).
internal static class H264AccessUnits
{
    public static async Task ReadAsync(Stream stream, Action<byte[]> emit, CancellationToken token)
    {
        var buffer = new byte[16384];
        using var nal = new MemoryStream();
        using var frame = new MemoryStream();
        var zeros = 0;
        var hasVideo = false;
        void FlushNal()
        {
            if (nal.Length == 0) return;
            var type = nal.GetBuffer()[0] & 31;
            if (type == 9 && hasVideo)
            {
                emit(frame.ToArray());
                frame.SetLength(0);
                hasVideo = false;
            }
            frame.Write(new byte[] { 0, 0, 0, 1 });
            frame.Write(nal.GetBuffer(), 0, (int)nal.Length);
            if (frame.Length > 4 * 1024 * 1024) throw new IOException("Video frame exceeds limit.");
            hasVideo |= type is 1 or 5;
            nal.SetLength(0);
        }
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0)
        {
            for (var i = 0; i < count; i++)
            {
                var value = buffer[i];
                if (value == 0) { if (++zeros > 4 * 1024 * 1024) throw new IOException("Video NAL exceeds limit."); continue; }
                if (value == 1 && zeros >= 2) { FlushNal(); zeros = 0; continue; }
                while (zeros > 0) { nal.WriteByte(0); zeros--; }
                nal.WriteByte(value);
                if (nal.Length > 4 * 1024 * 1024) throw new IOException("Video NAL exceeds limit.");
            }
        }
        FlushNal();
        if (hasVideo) emit(frame.ToArray());
    }
}
