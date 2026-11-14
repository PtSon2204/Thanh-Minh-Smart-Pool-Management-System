using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.IntegrationTests.ManageEquipments;

public sealed class CameraContinuousAnalysisTests
{
    [CameraFact]
    public async Task ContinuousDecoder_RefreshesPreviewAndHealth_WithoutPeriodicDecoderProcesses()
    {
        var ffmpeg = CameraTestEnvironment.GetRequiredFfmpeg();
        await using var rtsp = await LocalRtspCamera.StartAsync(ffmpeg);
        var options = Options.Create(new CameraOptions { FfmpegPath = ffmpeg, FrameStaleSeconds = 3,
            AllowedEndpoints = [new() { Host = "127.0.0.1", Port = rtsp.Port }] });
        var decoder = new CountingDecoder(new CameraFrameDecoder(options));
        await using var cameras = new CameraService(new CameraAddressPolicy(options), decoder,
            new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        var id = Guid.NewGuid();
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = rtsp.Url }, default))
        { registration.Publish(id); registration.Confirm(); }
        await using var source = new CameraFrameSource(cameras, options);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (source.Capture(id) == null && DateTimeOffset.UtcNow < deadline) await Task.Delay(20);
        Assert.NotNull(source.Capture(id));
        var count = decoder.Count;
        await Task.Delay(3500);
        Assert.Equal(count, decoder.Count);
        Assert.Equal("Connected", cameras.GetStatus(id).State);
        Assert.NotNull(cameras.GetFrame(id));
        Assert.InRange((DateTimeOffset.UtcNow - cameras.GetStatus(id).LastFrameAt!.Value).TotalSeconds, 0, 1);
        await source.DisposeAsync();
        deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (decoder.Count == count && DateTimeOffset.UtcNow < deadline) await Task.Delay(50);
        Assert.True(decoder.Count > count, "Snapshot decoder must resume after the continuous pump stops.");
    }

    private sealed class CountingDecoder(ICameraFrameDecoder inner) : ICameraFrameDecoder
    {
        private int count;
        public int Count => Volatile.Read(ref count);
        public Task<byte[]?> DecodeAsync(byte[] encoded, string format, CancellationToken ct)
        { Interlocked.Increment(ref count); return inner.DecodeAsync(encoded, format, ct); }
    }

    [CameraFact]
    public async Task AnalysisCapture_ConcurrentReadersAndDisposal_ShareOnePumpPerCamera()
    {
        var ffmpeg = CameraTestEnvironment.GetRequiredFfmpeg();
        await using var first = await LocalRtspCamera.StartAsync(ffmpeg);
        await using var second = await LocalRtspCamera.StartAsync(ffmpeg, color: "red");
        var options = Options.Create(new CameraOptions { FfmpegPath = ffmpeg,
            AllowedEndpoints = [new() { Host = "127.0.0.1", Port = first.Port }, new() { Host = "127.0.0.1", Port = second.Port }] });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options),
            new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        for (var index = 0; index < ids.Length; index++)
        {
            await using var registration = await cameras.PrepareAsync(new() { StreamUrl = index == 0 ? first.Url : second.Url }, default);
            registration.Publish(ids[index]); registration.Confirm();
        }
        await using var source = new CameraFrameSource(cameras, options);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(reader => Task.Run(async () => {
            for (var index = 0; index < 100; index++)
            {
                source.Capture(ids[(reader + index) % ids.Length]);
                await Task.Delay(1);
            }
        })));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (ids.Any(id => source.Capture(id) == null) && DateTimeOffset.UtcNow < deadline) await Task.Delay(20);
        Assert.All(ids, id => Assert.NotNull(source.Capture(id)));
        first.DisconnectClients();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(reader => Task.Run(async () => {
            for (var index = 0; index < 50; index++) { source.Capture(ids[reader % 2]); await Task.Delay(1); }
        })).Append(source.DisposeAsync().AsTask()));
        Assert.All(ids, id => Assert.Null(source.Capture(id)));
        Assert.Equal(1, second.AcceptedConnections);
    }

    [CameraTheory]
    [InlineData(1920, 1080, 1280, 720)]
    [InlineData(1920, 1200, 1152, 720)]
    [InlineData(320, 240, 320, 240)]
    public async Task AnalysisCapture_PreservesPreviewDetailAndAspectRatio_WithoutUpscaling(int width, int height, int expectedWidth, int expectedHeight)
    {
        var ffmpeg = CameraTestEnvironment.GetRequiredFfmpeg();
        await using var rtsp = await LocalRtspCamera.StartAsync(ffmpeg, width: width, height: height);
        var options = Options.Create(new CameraOptions { FfmpegPath = ffmpeg,
            AllowedEndpoints = [new() { Host = "127.0.0.1", Port = rtsp.Port }] });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options),
            new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        var id = Guid.NewGuid();
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = rtsp.Url }, default))
        { registration.Publish(id); registration.Confirm(); }
        await using var source = new CameraFrameSource(cameras, options);
        SmartPool.Application.Interfaces.Services.CameraCapturedFrame? frame = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (frame == null && DateTimeOffset.UtcNow < deadline)
        { frame = source.Capture(id); if (frame == null) await Task.Delay(20); }
        Assert.NotNull(frame);
        var jpeg = frame.Jpeg;
        // Đọc SOF của JPEG thật do FFmpeg xuất, không suy từ cấu hình mong đợi.
        for (var offset = 2; offset + 8 < jpeg.Length;)
        {
            Assert.Equal(0xff, jpeg[offset++]);
            var marker = jpeg[offset++];
            var length = (jpeg[offset] << 8) | jpeg[offset + 1];
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                Assert.Equal(expectedHeight, (jpeg[offset + 3] << 8) | jpeg[offset + 4]);
                Assert.Equal(expectedWidth, (jpeg[offset + 5] << 8) | jpeg[offset + 6]);
                return;
            }
            Assert.True(length >= 2);
            offset += length;
        }
        Assert.Fail("Không tìm thấy kích thước trong JPEG frame thực tế.");
    }

    [CameraFact]
    public async Task SimulatorReconnect_RotatesAnalysisSession_AndSupportsMultiplePaths()
    {
        var ffmpeg = CameraTestEnvironment.GetRequiredFfmpeg();
        await using var rtsp = await LocalRtspCamera.StartAsync(ffmpeg);
        var options = Options.Create(new CameraOptions { FfmpegPath = ffmpeg, ReconnectDelaySeconds = 10,
            AllowedEndpoints = [new() { Host = "127.0.0.1", Port = rtsp.Port, Simulator = true }] });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options),
            new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = rtsp.Url }, default))
        { registration.Publish(first); registration.Confirm(); }
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = rtsp.Url + "-other" }, default))
        { registration.Publish(second); registration.Confirm(); }
        var session = cameras.GetStatus(first).AnalysisSession;
        Assert.NotNull(session);
        Assert.NotEqual(session, cameras.GetStatus(second).AnalysisSession);
        rtsp.DisconnectClients();
        // Phải dùng retry simulator 250 ms, không rơi về delay 10 giây của camera vật lý.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(6);
        while (DateTimeOffset.UtcNow < deadline &&
            (cameras.GetStatus(first).State != "Connected" || cameras.GetStatus(first).AnalysisSession == session))
            await Task.Delay(100);
        Assert.Equal("Connected", cameras.GetStatus(first).State);
        Assert.NotEqual(session, cameras.GetStatus(first).AnalysisSession);
    }

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
