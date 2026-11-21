using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring.Audit;

public sealed class CameraAndAiAuditTests
{
    [Theory]
    [InlineData("D01", "H264", 4, true)]
    [InlineData("D02", "unknown", 4, true)]
    [InlineData("D03", "h264", 4194305, true)]
    [InlineData("D04", "h264", 4194304, false)]
    [InlineData("D05", "hevc", 4, false)]
    [InlineData("D06", "mjpeg", 0, false)]
    [Trait("Function", "CameraFrameDecoder.DecodeAsync")]
    public async Task DecodeAsync_GuardBoundary_RejectsOrRequiresConfigurationWithoutProcess(string utcId, string format, int length, bool returnsNull)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var decoder = new CameraFrameDecoder(Options.Create(new CameraOptions()));
        if (returnsNull) Assert.Null(await decoder.DecodeAsync(new byte[length], format, default));
        else { var ex = await Assert.ThrowsAsync<CameraConnectionException>(() => decoder.DecodeAsync(new byte[length], format, default));
            Assert.Equal("NotConfigured", ex.Code); }
    }

    [Theory]
    [InlineData("D07", false, 0, 1000)]
    [InlineData("D08", false, 1, 1000)]
    [InlineData("D09", false, 60, 60000)]
    [InlineData("D10", false, 61, 60000)]
    [InlineData("D11", true, 99, 100)]
    [InlineData("D12", true, 100, 100)]
    [InlineData("D13", true, 2000, 2000)]
    [InlineData("D14", true, 2001, 2000)]
    [Trait("Function", "CameraOptions.GetReconnectDelay")]
    public void GetReconnectDelay_ClampLimits_ReturnsBoundedDelay(string utcId, bool simulator, int configured, int milliseconds)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var options = new CameraOptions { ReconnectDelaySeconds = configured, SimulatorReconnectDelayMilliseconds = configured };
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), options.GetReconnectDelay(simulator));
    }

    [Theory]
    [InlineData("D15", "0.0.0.0")]
    [InlineData("D16", "169.254.1.2")]
    [InlineData("D17", "224.0.0.1")]
    [Trait("Function", "CameraAddressPolicy.ValidateAsync")]
    public async Task ValidateAsync_UnsafeLiteralEvenWhenAllowlisted_RejectsWithoutDns(string utcId, string host)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var policy = new CameraAddressPolicy(Options.Create(new CameraOptions { AllowedEndpoints = [new() { Host = host, Port = 554 }] }));
        var ex = await Assert.ThrowsAsync<CameraConnectionException>(() => policy.ValidateAsync(new() { StreamUrl = $"rtsp://{host}:554/live" }, default));
        Assert.Equal("AddressNotAllowed", ex.Code);
    }

    [Theory]
    [InlineData("D18", 14.999999, true)]
    [InlineData("D19", 15, true)]
    [InlineData("D20", 15.000001, false)]
    [Trait("Function", "LiveCameraSelection.Select")]
    public void Select_ReconnectGraceBoundary_RetainsRunningCameraThroughInclusiveLimit(string utcId, double age, bool retained)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var id = Guid.NewGuid(); var now = new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);
        var result = LiveCameraSelection.Select([(id, new CameraConnectionStatus("Disconnected", now.AddSeconds(-age)))],
            new HashSet<Guid> { id }, 1, now, TimeSpan.FromSeconds(15));
        Assert.Equal(retained ? new[] { id } : [], result);
    }

    [Theory]
    [InlineData("D21", "{\"detections\":[]}", "empty")]
    [InlineData("D22", "{\"detections\":null}", "empty")]
    [InlineData("D23", "{}", "missing")]
    [InlineData("D24", "{\"detections\":\"bad\"}", "invalid")]
    [InlineData("D25", "{\"detections\":[{\"trackId\":\"7\",\"class\":\"drowning\",\"confidence\":0.8,\"box\":[0,0,1,1]}]}", "valid")]
    [Trait("Function", "AiWorkerClient.Detections")]
    public void Detections_ProtocolResponse_ParsesOrReportsSchemaFailureWithoutWorker(string utcId, string json, string expected)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        using var document = JsonDocument.Parse(json);
        // Match the repository's existing reflection approach for internal pure parsers.
        var method = typeof(MonitoringOptions).Assembly.GetType("SmartPool.Infrastructure.SafetyMonitoring.AiWorkerClient")!
            .GetMethod("Detections", BindingFlags.Public | BindingFlags.Static)!;
        if (expected is "missing" or "invalid") {
            var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [document]));
            if (expected == "missing") Assert.IsType<KeyNotFoundException>(error.InnerException);
            else Assert.IsType<JsonException>(error.InnerException);
        } else {
            var result = (TrackedDetection[])method.Invoke(null, [document])!;
            if (expected == "empty") Assert.Empty(result);
            else { var detection = Assert.Single(result); Assert.Equal("7", detection.TrackId); Assert.Equal(.8, detection.Confidence); Assert.Equal(new double[] { 0, 0, 1, 1 }, detection.Box); }
        }
    }
}
