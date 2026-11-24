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
    public bool Failed => stop.IsCancellationRequested || run.IsCompleted;
    public string? Failure { get; private set; }//code ngoài class có thể đọc nhưng không thể ghi, chỉ có class này mới có thể ghi
    public CameraCapturedFrame? Latest => Volatile.Read(ref latest);

    public CameraAnalysisFramePump(CameraService cameras, Guid id, CameraOptions options)
    {
        subscription = cameras.Subscribe(id, Offer, continuousDecoder: true);
        run = Task.Run(() => RunAsync(options, cameras, id));
    }

    private void Offer(CameraEncodedFrame frame)
    {
        if (stop.IsCancellationRequested) return;
        if (Interlocked.Add(ref queuedBytes, frame.Data.Length) > 4 * 1024 * 1024 ||
            !input.Writer.TryWrite((frame, frame.CapturedAt ?? DateTimeOffset.UtcNow)))
        {
            Failure = "InputBacklog";
            Interlocked.Add(ref queuedBytes, -frame.Data.Length);
            // Dropping arbitrary P frames corrupts decoding. Restart at a fresh keyframe instead.
            stop.Cancel();
        }
    }

    private async Task RunAsync(CameraOptions options, CameraService cameras, Guid id)
    {
        Process? process = null;
        Task output = Task.CompletedTask, errors = Task.CompletedTask;
        var pending = new ConcurrentQueue<(DateTimeOffset Time, uint RtpTimestamp)>();
        //lưu trữ các khung hình đang chờ xử lý, bao gồm thời gian quan sát và timestamp RTP, concurrent queue để đảm bảo thread-safe khi nhiều luồng truy cập first-in-first-out
        try
        {
            await foreach (var item in input.Reader.ReadAllAsync(stop.Token))
            {
                Interlocked.Add(ref queuedBytes, -item.Frame.Data.Length);
                if (process is null)
                {
                    if (!item.Frame.IsKeyFrame) continue;
                    var start = new ProcessStartInfo(options.FfmpegPath)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };//khởi tạo một tiến trình FFmpeg mới để giải mã khung hình camera, với các thiết lập để không hiển thị cửa sổ và chuyển hướng đầu vào/đầu ra
                    // Giữ chi tiết cho preview/snapshot; model tự resize tensor riêng.
                    // Không upscale nguồn thấp hoặc làm méo tỉ lệ hình ảnh.
                    var width = Math.Clamp(options.AnalysisMaxWidth, 32, 3840);
                    var height = Math.Clamp(options.AnalysisMaxHeight, 32, 2160);
                    var scale = $"scale=w='min({width},iw)':h='min({height},ih)':force_original_aspect_ratio=decrease:force_divisible_by=2";//tạo chuỗi lệnh để thay đổi kích thước khung hình đầu vào của FFmpeg, giữ nguyên tỷ lệ khung hình gốc và đảm bảo chiều rộng và chiều cao là bội số của 2
                    foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-xerror", "-err_detect", "explode", "-protocol_whitelist", "pipe",
                        // Keep AVIO buffering: direct mode loses H.264 probe/header
                        // bytes on older FFmpeg builds, causing SPS/PPS errors and restarts.
                        // The codec is explicit and keyframes include parameter sets;
                        // use the minimum probe budget so tiny streams also restart promptly.
                        "-blocksize", "4096",
                        "-analyzeduration", "1", "-probesize", "32", "-fpsprobesize", "0", "-threads", "1", "-filter_threads", "1", "-flags", "low_delay",
                        "-f", item.Frame.Format, "-i", "pipe:0", "-an", "-vf", scale, "-fps_mode", "passthrough",
                        "-c:v", "mjpeg", "-q:v", Math.Clamp(options.AnalysisJpegQscale, 2, 31).ToString(), "-threads", "1", "-flush_packets", "1", "-f", "image2pipe", "pipe:1" })
                        start.ArgumentList.Add(arg);
                    process = Process.Start(start) ?? throw new IOException("Camera analysis decoder unavailable.");
                    errors = WatchErrorsAsync(process.StandardError);
                    output = ReadJpegsAsync(process.StandardOutput.BaseStream, jpeg =>//baseStream: luồng dữ liệu nhị phân của tiến trình FFmpeg, nơi các khung hình JPEG đã giải mã được đọc và truyền đến hành động emit để xử lý
                    {
                        if (stop.IsCancellationRequested) return;
                        // Timestamps describe incoming frames, not the end of inference.
                        if (!pending.TryDequeue(out var observed)) { Failure = "FrameAlignment"; throw new IOException("Camera decoder lost frame alignment."); }
                        Volatile.Write(ref latest, new CameraCapturedFrame(jpeg, observed.Time, Guid.NewGuid().ToString("N"), observed.RtpTimestamp));
                        // Reuse the continuous decoder for health/preview too; avoid spawning
                        // another FFmpeg every two seconds for the same camera.
                        cameras.UpdateDecodedFrame(id, jpeg, observed.Time);
                    }, stop.Token);
                }
                if (process.HasExited || output.IsCompleted || pending.Count >= 128)
                {
                    Failure ??= process.HasExited ? $"DecoderExit:{process.ExitCode}" : output.IsCompleted ?
                        $"JpegOutputStopped:{output.Exception?.GetBaseException().GetType().Name ?? "EOF"}" : "PendingBacklog";
                    throw new IOException("Camera analysis decoder stalled.");
                }
                pending.Enqueue((item.Time, item.Frame.RtpTimestamp));
                await process.StandardInput.BaseStream.WriteAsync(item.Frame.Data, stop.Token);
                await process.StandardInput.BaseStream.FlushAsync(stop.Token);
            }
        }
        catch (Exception exception) { if (!stop.IsCancellationRequested) Failure ??= exception.GetType().Name; }
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
        var buffer = new byte[16384];//tạo một mảng byte có kích thước 16KB để đọc dữ liệu từ luồng đầu ra của tiến trình FFmpeg, buffer này sẽ được sử dụng để lưu trữ các khối dữ liệu JPEG đã giải mã trước khi xử lý
        using var frame = new MemoryStream();//tạo một MemoryStream để lưu trữ dữ liệu của khung hình JPEG hiện tại đang được đọc từ luồng đầu ra của tiến trình FFmpeg, MemoryStream này sẽ được sử dụng để xây dựng khung hình JPEG hoàn chỉnh trước khi truyền đến hành động emit để xử lý
        var startMarker = new byte[] { 0xff, 0xd8 };
        var endMarker = new byte[] { 0xff, 0xd9 };
        int count;
        bool started = false, carryFF = false;//ghi nhớ maker byte ff nằm giữa 2 lần đọc
        while ((count = await stream.ReadAsync(buffer, ct)) > 0)
        {
            var offset = 0;//offset: chỉ số hiện tại trong buffer, bắt đầu từ 0 và được tăng lên khi đọc dữ liệu từ buffer
            if (!started && carryFF && buffer[0] == 0xd8)
            {
                frame.WriteByte(0xff); started = true; carryFF = false;
            }
            while (offset < count)
            {
                if (!started)
                {
                    var start = buffer.AsSpan(offset, count - offset).IndexOf(startMarker);//aspan(start, length)
                    if (start < 0) { carryFF = buffer[count - 1] == 0xff; break; }
                    offset += start; started = true; carryFF = false;
                }
                if (carryFF && buffer[offset] == 0xd9)
                {
                    if (frame.Length >= 4 * 1024 * 1024) throw new IOException("Decoded camera frame exceeds limit.");
                    frame.WriteByte(0xd9); offset++;
                    emit(frame.ToArray()); frame.SetLength(0); started = false; carryFF = false;
                    continue;
                }
                var end = buffer.AsSpan(offset, count - offset).IndexOf(endMarker);
                var length = end < 0 ? count - offset : end + 2;
                if (frame.Length + length > 4 * 1024 * 1024) throw new IOException("Decoded camera frame exceeds limit.");
                // Copy JPEG payload in blocks; avoid millions of WriteByte calls for 720p video.
                frame.Write(buffer, offset, length); offset += length;
                carryFF = buffer[offset - 1] == 0xff;
                if (end < 0) break;
                emit(frame.ToArray()); frame.SetLength(0); started = false; carryFF = false;
            }
        }
    }

    private async Task WatchErrorsAsync(StreamReader reader)
    {
        // A decoder may silently skip the bad picture and keep emitting JPEGs.
        // That would shift every subsequent FIFO timestamp. Stop immediately on
        // error-level diagnostics; never log their contents or reuse this pump.
        while (await reader.ReadLineAsync(stop.Token) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            Failure = "DecodeError";
            Volatile.Write(ref latest, null);//ghi đè giá trị của latest bằng null, đảm bảo rằng các luồng khác sẽ thấy giá trị mới nhất là null và không sử dụng khung hình đã giải mã trước đó
            stop.Cancel();
            return;
        }
    }

    public async ValueTask DisposeAsync()
    {
        subscription.Dispose(); stop.Cancel(); input.Writer.TryComplete();
        try { await run.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
    }
}
