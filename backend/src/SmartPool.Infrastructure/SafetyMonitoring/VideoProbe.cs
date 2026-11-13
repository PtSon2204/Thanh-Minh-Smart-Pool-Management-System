using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed record VideoInfo(double Fps, double Duration, int? FrameCount = null);
public sealed class VideoProbe(IOptions<MonitoringOptions> options)
{
    public VideoTimeline OpenTimeline(string path, VideoInfo info) => new(options.Value.FfprobePath, path, info);

    public async Task<VideoInfo> InspectAsync(string path, bool verifyTimeline, CancellationToken ct)
    {
        if (!File.Exists(options.Value.FfprobePath)) throw new InvalidOperationException("Chưa cấu hình FFprobe để kiểm tra nội dung video.");
        var start = new ProcessStartInfo(options.Value.FfprobePath) { CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        var args = new List<string> { "-v", "error", "-threads", "1", "-protocol_whitelist", "file,pipe", "-format_whitelist", "mov,avi,matroska,webm",
            "-select_streams", "v:0", "-show_streams", "-show_format" };
        if (verifyTimeline) args.AddRange(["-show_frames", "-show_entries", "frame=best_effort_timestamp_time:stream=codec_name,width,height,avg_frame_rate,duration:format=format_name,duration"]);
        args.AddRange(["-of", "json", path]);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Không khởi động được trình kiểm tra video.");
        if (verifyTimeline) try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception) { }
        using var cancel = ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (Exception) { } });
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var output = await outputTask; await errors;
        if (process.ExitCode != 0 || output.Length > 16 * 1024 * 1024) throw new ArgumentException("Không đọc được nội dung video.");
        using var json = JsonDocument.Parse(output);
        var streams = json.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() != 1) throw new ArgumentException("Video không có luồng hình ảnh hợp lệ.");
        var stream = streams[0];
        var codec = stream.GetProperty("codec_name").GetString();
        if (codec is not ("h264" or "hevc" or "mpeg4" or "mjpeg" or "vp8" or "vp9" or "av1")) throw new ArgumentException("Codec video chưa được hỗ trợ.");
        var width = stream.GetProperty("width").GetInt32(); var height = stream.GetProperty("height").GetInt32();
        var rate = stream.GetProperty("avg_frame_rate").GetString()!.Split('/');
        var fps = double.Parse(rate[0], CultureInfo.InvariantCulture) / double.Parse(rate[1], CultureInfo.InvariantCulture);
        var format = json.RootElement.GetProperty("format");
        var duration = double.Parse(format.GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        if (!double.IsFinite(fps) || fps is < 1 or > 60 || !double.IsFinite(duration) || duration <= 0 || duration > options.Value.MaxVideoSeconds ||
            width < 2 || height < 2 || width > 3840 || height > 2160 || width % 2 != 0 || height % 2 != 0)
            throw new ArgumentException($"Video phải có kích thước chẵn tối đa 3840×2160, 1–60 FPS và thời lượng tối đa {options.Value.MaxVideoSeconds} giây.");
        int? verifiedFrames = null;
        if (verifyTimeline)
        {
            var index = 0;
            double? first = null;
            foreach (var frame in json.RootElement.GetProperty("frames").EnumerateArray())
            {
                if (!frame.TryGetProperty("best_effort_timestamp_time", out var time)) throw new ArgumentException("Video không có timeline hợp lệ.");
                var timestamp = double.Parse(time.GetString()!, CultureInfo.InvariantCulture);
                first ??= timestamp;
                if (Math.Abs(timestamp - first.Value - index / fps) > Math.Max(0.005, 0.1 / fps))
                    throw new ArgumentException("Video có tốc độ khung hình biến đổi (VFR). Hãy chuyển sang CFR trước khi phân tích.");
                index++;
            }
            if (index < 2 || Math.Abs(index / fps - duration) > Math.Max(0.25, 2 / fps))
                throw new ArgumentException("Timeline hoặc thời lượng video không được hỗ trợ.");
            verifiedFrames = index;
        }
        return new(fps, duration, verifiedFrames);
    }
}

/// <summary>Validate the decoded CFR timeline one frame at a time, before publishing
/// that frame's AI results. Avoid scanning the entire clip before its first frame.</summary>
public sealed class VideoTimeline : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task<string> errors;
    private readonly VideoInfo info;
    private int index;
    private double? first;
    public VideoTimeline(string executable, string path, VideoInfo info)
    {
        this.info = info;
        var start = new ProcessStartInfo(executable) { CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-threads", "1", "-protocol_whitelist", "file,pipe", "-format_whitelist", "mov,avi,matroska,webm",
            "-select_streams", "v:0", "-show_frames", "-show_entries", "frame=best_effort_timestamp_time", "-of", "compact=p=0", path }) start.ArgumentList.Add(arg);
        process = Process.Start(start) ?? throw new IOException("Không kiểm tra được timeline video.");
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception) { }
        errors = process.StandardError.ReadToEndAsync();
    }
    public async Task<bool> ReadAsync(CancellationToken ct)
    {
        string? line;
        do { line = await process.StandardOutput.ReadLineAsync(ct); } while (line != null && string.IsNullOrWhiteSpace(line));
        if (line == null)
        {
            await process.WaitForExitAsync(ct); await errors;
            if (process.ExitCode != 0 || index < 2 || Math.Abs(index / info.Fps - info.Duration) > Math.Max(0.25, 2 / info.Fps))
                throw new ArgumentException("Timeline hoặc thời lượng video không được hỗ trợ.");
            return false;
        }
        const string prefix = "best_effort_timestamp_time=";
        var value = line.Split('|', 2)[0];
        if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
            !double.TryParse(value.AsSpan(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var timestamp) || !double.IsFinite(timestamp))
            throw new ArgumentException("Video không có timeline hợp lệ.");
        first ??= timestamp;
        if (index >= 40000 || Math.Abs(timestamp - first.Value - index / info.Fps) > Math.Max(0.005, 0.1 / info.Fps))
            throw new ArgumentException("Video có tốc độ khung hình biến đổi (VFR). Hãy chuyển sang CFR trước khi phân tích.");
        index++;
        return true;
    }
    public async ValueTask DisposeAsync()
    {
        try { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); await errors; }
        finally { process.Dispose(); }
    }
}
