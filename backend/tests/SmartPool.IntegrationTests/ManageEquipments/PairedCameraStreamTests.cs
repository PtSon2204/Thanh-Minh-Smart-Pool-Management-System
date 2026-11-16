using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.SafetyMonitoring;
using SmartPool.IntegrationTests.SafetyMonitoring;

namespace SmartPool.IntegrationTests.ManageEquipments;

public sealed partial class CameraStreamEquipmentTests
{
    [CameraFact]
    public async Task SimulatorReconnect_KeepsExistingWebRtcView_AndResumesFramesWithoutNewOffer()
    {
        var ffmpeg = CameraTestEnvironment.GetRequiredFfmpeg();
        await using var rtsp = await LocalRtspCamera.StartAsync(ffmpeg);
        var settings = Options.Create(new CameraOptions { FfmpegPath = ffmpeg,
            AllowedEndpoints = [new() { Host = "127.0.0.1", Port = rtsp.Port, Simulator = true }] });
        await using var cameras = new CameraService(new CameraAddressPolicy(settings), new CameraFrameDecoder(settings),
            new EphemeralDataProtectionProvider(), settings, TimeProvider.System, NullLogger<CameraService>.Instance);
        var id = Guid.NewGuid();
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = rtsp.Url }, default))
        { registration.Publish(id); registration.Confirm(); }
        using var streams = new CameraStreamService(cameras, settings);
        await streams.StartAsync(default);
        using var peer = new RTCPeerConnection(new RTCConfiguration { X_ICEIncludeAllInterfaceAddresses = true, X_GatherTimeoutMs = 10000 });
        peer.addTrack(new MediaStreamTrack(new VideoFormat(VideoCodecsEnum.H264, 96, 90000,
            "packetization-mode=1;profile-level-id=42e01f;level-asymmetry-allowed=1"), MediaStreamStatusEnum.RecvOnly));
        var frames = 0;
        peer.OnVideoFrameReceived += (_, _, _, _) => Interlocked.Increment(ref frames);
        var offer = peer.createOffer(new RTCOfferOptions { X_WaitForIceGatheringToComplete = true });
        await peer.setLocalDescription(offer);
        var view = await streams.OpenAsync(id, "reconnect-test", new("offer", offer.sdp), default);
        try
        {
            Assert.Equal(SetDescriptionResultEnum.OK, peer.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = view.Sdp }));
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (Volatile.Read(ref frames) == 0 && DateTimeOffset.UtcNow < deadline) await Task.Delay(20);
            Assert.True(frames > 0);
            var session = cameras.GetStatus(id).AnalysisSession;
            rtsp.DisconnectClients();
            deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (cameras.GetStatus(id).State == "Connected" && DateTimeOffset.UtcNow < deadline) await Task.Delay(10);
            Assert.Contains((await streams.GetStatusAsync(id, view.ViewId, "reconnect-test", default)).State, new[] { "Connecting", "Connected" });
            deadline = DateTimeOffset.UtcNow.AddSeconds(6);
            while ((cameras.GetStatus(id).State != "Connected" || cameras.GetStatus(id).AnalysisSession == session) && DateTimeOffset.UtcNow < deadline) await Task.Delay(20);
            Assert.Equal("Connected", cameras.GetStatus(id).State);
            Assert.NotEqual(session, cameras.GetStatus(id).AnalysisSession);
            var before = Volatile.Read(ref frames);
            while (Volatile.Read(ref frames) <= before && DateTimeOffset.UtcNow < deadline) await Task.Delay(20);
            Assert.True(frames > before);
            Assert.Equal("Connected", (await streams.GetStatusAsync(id, view.ViewId, "reconnect-test", default)).State);
        }
        finally { await streams.CloseAsync(id, view.ViewId, "reconnect-test"); await streams.StopAsync(default); }
    }

    [NativeSafetyFact]
    public async Task PairedWebRtc_SendsAnalyzedPixels_KeepsRawPreviewSeparate_WithoutSecondRtspConnection()
    {
        var ffmpeg = CameraTestEnvironment.GetRequiredFfmpeg();
        await using var blue = await LocalRtspCamera.StartAsync(ffmpeg);
        await using var red = await LocalRtspCamera.StartAsync(ffmpeg, color: "red");
        var settings = Options.Create(new CameraOptions { FfmpegPath = ffmpeg,
            AllowedEndpoints = [new() { Host = "127.0.0.1", Port = blue.Port }, new() { Host = "127.0.0.1", Port = red.Port }] });
        await using var cameras = new CameraService(new CameraAddressPolicy(settings), new CameraFrameDecoder(settings),
            new EphemeralDataProtectionProvider(), settings, TimeProvider.System, NullLogger<CameraService>.Instance);
        var blueId = Guid.NewGuid(); var redId = Guid.NewGuid();
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = blue.Url }, default))
        { registration.Publish(blueId); registration.Confirm(); }
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = red.Url }, default))
        { registration.Publish(redId); registration.Confirm(); }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "README.md"))) root = root.Parent;
        Assert.NotNull(root);
        var monitoring = Options.Create(new MonitoringOptions { Enabled = true,
            PythonPath = Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_PYTHON")!,
            WorkerPath = Path.Combine(root.FullName, "backend", "ai", "worker.py"), ModelPath = Path.Combine(root.FullName, "model", "best.onnx") });
        Assert.True(monitoring.Value.Configured);
        var analysis = new CameraAnalysisState(cameras, monitoring);
        var analyzedPixels = cameras.GetFrame(redId)!;
        using var publishing = new CancellationTokenSource();
        // Màu đỏ đại diện pixel đã phân tích, khác màu xanh của stream gốc.
        // Không chạy RiskEngine hoặc tạo bất kỳ Alert nào trong test transport này.
        var pump = Task.Run(async () => {
            try {
                while (!publishing.IsCancellationRequested) {
                    analysis.Set(blueId, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, [], analyzedPixels);
                    await Task.Delay(80, publishing.Token);
                }
            } catch (OperationCanceledException) when (publishing.IsCancellationRequested) { }
        });
        using var streams = new CameraStreamService(cameras, settings, analysis);
        await streams.StartAsync(default);
        var clients = new List<RTCPeerConnection>();
        var sourceTimestamps = new System.Collections.Concurrent.ConcurrentDictionary<uint, bool>();
        await using var captured = new CameraFrameSource(cameras, settings);
        var clockPump = Task.Run(async () => {
            try {
                while (!publishing.IsCancellationRequested) {
                    if (captured.Capture(blueId)?.RtpTimestamp is uint timestamp) sourceTimestamps.TryAdd(timestamp, true);
                    await Task.Delay(10, publishing.Token);
                }
            } catch (OperationCanceledException) when (publishing.IsCancellationRequested) { }
        });
        try
        {
            async Task<(CameraView View, byte[] Pixel)> Open(bool paired, bool smooth = false)
            {
                var peer = new RTCPeerConnection(new RTCConfiguration { X_ICEIncludeAllInterfaceAddresses = true, X_GatherTimeoutMs = 10000 });
                clients.Add(peer);
                peer.addTrack(new MediaStreamTrack(new VideoFormat(VideoCodecsEnum.H264, 96, 90000,
                    "packetization-mode=1;profile-level-id=42e01f;level-asymmetry-allowed=1"), MediaStreamStatusEnum.RecvOnly));
                var frame = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var delta = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var receivedTimestamps = new System.Collections.Concurrent.ConcurrentDictionary<uint, bool>();
                peer.OnVideoFrameReceived += (_, timestamp, bytes, _) => {
                    receivedTimestamps.TryAdd(timestamp, true);
                    if (H264Packetiser.ParseNals(bytes).Any(n => (n.NAL[0] & 31) == 5)) frame.TrySetResult(bytes);
                    if (H264Packetiser.ParseNals(bytes).Any(n => (n.NAL[0] & 31) == 1)) delta.TrySetResult();
                };
                var offer = peer.createOffer(new RTCOfferOptions { X_WaitForIceGatheringToComplete = true });
                await peer.setLocalDescription(offer);
                var view = await streams.OpenAsync(blueId, "paired-test", new("offer", offer.sdp, paired, smooth), default);
                Assert.Equal(SetDescriptionResultEnum.OK, peer.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = view.Sdp }));
                var pixel = await PixelAsync(ffmpeg, await frame.Task.WaitAsync(TimeSpan.FromSeconds(30)));
                // AI preview phải phát cả P-frame để tránh truyền lại toàn ảnh ở mỗi frame.
                if (paired && !smooth) await delta.Task.WaitAsync(TimeSpan.FromSeconds(10));
                if (smooth) {
                    var deadline = DateTime.UtcNow.AddSeconds(10);
                    while (!receivedTimestamps.Keys.Any(sourceTimestamps.ContainsKey) && DateTime.UtcNow < deadline) await Task.Delay(20);
                    Assert.Contains(receivedTimestamps.Keys, sourceTimestamps.ContainsKey);
                }
                return (view, pixel);
            }
            var raw = await Open(false);
            var paired = await Open(true);
            Assert.True(raw.Pixel[2] > raw.Pixel[0] + 100, "Raw preview must keep the original blue pixels.");
            Assert.True(paired.Pixel[0] > paired.Pixel[2] + 100, "AI preview must send the analyzed red pixels, not raw video plus late metadata.");
            Assert.Equal(1, blue.AcceptedConnections);
            Assert.True(cameras.GetStatus(blueId).SmoothPlayback);
            var smooth = await Open(true, true);
            Assert.True(smooth.Pixel[2] > smooth.Pixel[0] + 100, "Smooth AI playback must share the source video clock and original pixels.");
            await streams.CloseAsync(blueId, smooth.View.ViewId, "paired-test");
            await streams.CloseAsync(blueId, raw.View.ViewId, "paired-test");
            Assert.Equal("Connected", (await streams.GetStatusAsync(blueId, paired.View.ViewId, "paired-test", default)).State);
            await streams.CloseAsync(blueId, paired.View.ViewId, "paired-test");
        }
        finally
        {
            publishing.Cancel(); await Task.WhenAll(pump, clockPump);
            foreach (var peer in clients) { peer.Close("Test finished"); peer.Dispose(); }
            await streams.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
