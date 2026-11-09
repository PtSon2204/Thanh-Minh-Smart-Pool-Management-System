using System.Diagnostics;
using System.Text.Json;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

internal sealed class AiWorkerClient : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task drain;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AiWorkerClient(MonitoringOptions options, bool lowPriority = false)
    {
        if (!options.Configured) throw new InvalidOperationException("AI chưa được cấu hình.");
        var start = new ProcessStartInfo(options.PythonPath) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-u", options.WorkerPath, options.ModelPath, options.FontPath }) start.ArgumentList.Add(arg);
        start.Environment["OMP_NUM_THREADS"] = "1"; start.Environment["MKL_NUM_THREADS"] = "1";
        start.Environment["YOLO_AUTOINSTALL"] = "false";
        process = Process.Start(start) ?? throw new InvalidOperationException("Không khởi động được AI worker.");
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
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, Json).AsMemory(), ct);
        await process.StandardInput.FlushAsync(ct);
        return await ReadAsync(ct);
    }
    public static TrackedDetection[] Detections(JsonDocument result) =>
        result.RootElement.GetProperty("detections").Deserialize<TrackedDetection[]>(Json) ?? [];

    private async Task<JsonDocument> ReadAsync(CancellationToken ct)
    {
        var line = await process.StandardOutput.ReadLineAsync(ct) ?? throw new IOException("AI worker đã ngừng hoạt động.");
        if (line.Length > 16 * 1024 * 1024) throw new IOException("AI response exceeds limit.");
        var document = JsonDocument.Parse(line);
        if (document.RootElement.TryGetProperty("error", out _)) { document.Dispose(); throw new IOException("AI worker xử lý thất bại."); }
        return document;
    }

    public async ValueTask DisposeAsync()
    {
        try { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); await drain; }
        catch (InvalidOperationException) { }
        process.Dispose();
    }
}
