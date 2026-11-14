using System.Diagnostics;
using System.Buffers;
using System.IO.Pipelines;
using System.Text.Json;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

internal sealed class AiWorkerClient : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task drain;
    private readonly PipeReader output;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AiWorkerClient(MonitoringOptions options, bool lowPriority = false, int? inferenceThreads = null)
    {
        if (!options.Configured) throw new InvalidOperationException("AI chưa được cấu hình.");
        var start = new ProcessStartInfo(options.PythonPath) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-u", options.WorkerPath, options.ModelPath, options.FontPath }) start.ArgumentList.Add(arg);
        var threads = Math.Clamp(inferenceThreads ?? (lowPriority ? 4 : options.LiveInferenceThreads), 1, Math.Max(1, Environment.ProcessorCount));
        start.Environment["OMP_NUM_THREADS"] = threads.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["MKL_NUM_THREADS"] = threads.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["SAFETY_ORT_PROVIDER"] = options.InferenceProviders;
        start.Environment["YOLO_AUTOINSTALL"] = "false";
        process = Process.Start(start) ?? throw new InvalidOperationException("Không khởi động được AI worker.");
        output = PipeReader.Create(process.StandardOutput.BaseStream);
        if (lowPriority) try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception) { }
        drain = Task.Run(async () => { var buffer = new char[4096]; while (await process.StandardError.ReadAsync(buffer) > 0) { } });
    }

    public async Task ReadyAsync(CancellationToken ct)
    {
        using var result = await ReadAsync(ct);
        if (!result.RootElement.TryGetProperty("ready", out var ready) || !ready.GetBoolean()) throw new IOException("AI worker không sẵn sàng.");
    }
    public async Task<JsonDocument> SendAsync(object request, CancellationToken ct)
    {
        await WriteRequestAsync(request, ct);
        return await ReadAsync(ct);
    }
    public async Task<JsonDocument> InferAsync(Guid cameraId, byte[] jpeg, bool resetTracker, CancellationToken ct)
    {
        if (jpeg.Length is < 4 or > 4 * 1024 * 1024) throw new IOException("Invalid AI frame size.");
        await WriteRequestAsync(new { op = "infer", key = cameraId.ToString(), resetTracker, jpegLength = jpeg.Length }, ct);
        await process.StandardInput.BaseStream.WriteAsync(jpeg, ct);
        await process.StandardInput.BaseStream.FlushAsync(ct);
        return await ReadAsync(ct);
    }
    public async Task<byte[]> RenderAsync(IReadOnlyList<RiskDecision> decisions, CancellationToken ct)
    {
        using var response = await SendAsync(new { op = "render", decisions, binary = true }, ct);
        var length = response.RootElement.GetProperty("jpegLength").GetInt32();
        if (length is < 4 or > 4 * 1024 * 1024) throw new IOException("Invalid AI image size.");
        var bytes = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var result = await output.ReadAsync(ct);
            var count = (int)Math.Min(length - offset, result.Buffer.Length);
            result.Buffer.Slice(0, count).CopyTo(bytes.AsSpan(offset));
            offset += count;
            output.AdvanceTo(result.Buffer.GetPosition(count));
            if (count == 0 && result.IsCompleted) throw new IOException("AI image was truncated.");
        }
        return bytes;
    }
    private async Task WriteRequestAsync(object request, CancellationToken ct)
    {
        await process.StandardInput.BaseStream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(request, Json), ct);
        await process.StandardInput.BaseStream.WriteAsync(new byte[] { 10 }, ct);
        await process.StandardInput.BaseStream.FlushAsync(ct);
    }
    public static TrackedDetection[] Detections(JsonDocument result) =>
        result.RootElement.GetProperty("detections").Deserialize<TrackedDetection[]>(Json) ?? [];

    private async Task<JsonDocument> ReadAsync(CancellationToken ct)
    {
        while (true)
        {
            var result = await output.ReadAsync(ct);
            var buffer = result.Buffer;
            if (buffer.Length > 16 * 1024 * 1024) throw new IOException("AI response exceeds limit.");
            var end = buffer.PositionOf((byte)10);
            if (end.HasValue)
            {
                var document = JsonDocument.Parse(buffer.Slice(0, end.Value).ToArray());
                output.AdvanceTo(buffer.GetPosition(1, end.Value));
                if (document.RootElement.TryGetProperty("error", out _)) { document.Dispose(); throw new IOException("AI worker xử lý thất bại."); }
                return document;
            }
            if (result.IsCompleted) throw new IOException("AI worker đã ngừng hoạt động.");
            output.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); await drain; }
        catch (InvalidOperationException) { }
        await output.CompleteAsync();
        process.Dispose();
    }
}
