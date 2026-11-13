using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.IntegrationTests.ManageEquipments;

public sealed class CameraContinuousAnalysisTests
{
    [CameraFact]
    public async Task AnalysisCapture_ReceivesContinuousFramesFromSharedRtsp_WithoutTwoSecondSampling()
    {
        var ffmpeg = CameraTestEnvironment.GetRequiredFfmpeg();
        await using var rtsp = await LocalRtspCamera.StartAsync(ffmpeg);
        var options = Options.Create(new CameraOptions { FfmpegPath = ffmpeg,
            AllowedEndpoints = [new() { Host = "127.0.0.1", Port = rtsp.Port }] });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options),
            new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        var id = Guid.NewGuid();
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = rtsp.Url }, default))
        {
            registration.Publish(id); registration.Confirm();
        }
        await using var source = new CameraFrameSource(cameras, options);
        var seen = new HashSet<string>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < deadline && seen.Count < 10)
        {
            var frame = source.Capture(id);
            if (frame is not null)
            {
                Assert.True(frame.Jpeg.Length > 4);
                Assert.Equal(0xff, frame.Jpeg[0]); Assert.Equal(0xd8, frame.Jpeg[1]);
                Assert.InRange((DateTimeOffset.UtcNow - frame.CapturedAt).TotalSeconds, 0, 2);
                seen.Add(frame.FrameId);
            }
            await Task.Delay(20);
        }
        Assert.True(seen.Count >= 10, $"Expected continuous samples; received {seen.Count} distinct frames.");
        Assert.Equal(1, rtsp.AcceptedConnections);
    }
}
