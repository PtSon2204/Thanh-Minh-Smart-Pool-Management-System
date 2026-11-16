using System.Reflection;
using System.Text.Json;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    [NativeSafetyFact]
    public async Task BinaryAiTransport_RendersWarningPixels_AndReadsTheNextJsonHeaderWithoutLosingAlignment()
    {
        var settings = new MonitoringOptions { Enabled = true,
            PythonPath = Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_PYTHON")!,
            WorkerPath = Path.Combine(RepositoryRoot(), "backend/ai/worker.py"),
            ModelPath = Path.Combine(RepositoryRoot(), "model/best.onnx"),
            FontPath = Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_FONT")! };
        var type = typeof(CameraAnalysisState).Assembly.GetType("SmartPool.Infrastructure.SafetyMonitoring.AiWorkerClient")!;
        await using var worker = (IAsyncDisposable)Activator.CreateInstance(type, settings, false, (int?)1)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await ((Task)type.GetMethod("ReadyAsync")!.Invoke(worker, [timeout.Token])!).WaitAsync(timeout.Token);
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot(), "frontend/thanh-minh-smart-pool/src/assets/anh-be-boi.jpg"));
        var id = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            using var result = await (Task<JsonDocument>)type.GetMethod("InferAsync")!.Invoke(worker, [id, jpeg, i == 0, timeout.Token])!;
            Assert.True(result.RootElement.TryGetProperty("detections", out _));
            RiskDecision[] boxes = [new("transport-test", "render-only", "WARNING", .8, 0, [.1, .1, .5, .8])];
            var rendered = await ((Task<byte[]>)type.GetMethod("RenderAsync")!.Invoke(worker, [boxes, timeout.Token])!).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(rendered.Length > 4);
            Assert.Equal(0xff, rendered[0]); Assert.Equal(0xd8, rendered[1]);
            Assert.Equal(0xff, rendered[^2]); Assert.Equal(0xd9, rendered[^1]);
            using var next = await (Task<JsonDocument>)type.GetMethod("SendAsync")!.Invoke(worker, [new { op = "capture" }, timeout.Token])!;
            Assert.NotEmpty(next.RootElement.GetProperty("jpeg").GetString()!);
        }
    }
}
