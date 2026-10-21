using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Cameras;

namespace SmartPool.IntegrationTests;

public sealed class CameraStreamEquipmentTests
{
    [Fact]
    public async Task WebRtc_UsesCorrectCameraSharesSourceEnforcesOwnershipAndReleasesOnlyViewer()
    {
        var ffmpeg = Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")!;
        await using var blue = await LocalRtspCamera.StartAsync(ffmpeg, hevc: true);
        await using var red = await LocalRtspCamera.StartAsync(ffmpeg, color: "red");
        var options = Options.Create(new CameraOptions
        {
            AllowedEndpoints = [new() { Host = "127.0.0.1", Port = blue.Port }, new() { Host = "127.0.0.1", Port = red.Port }],
            FfmpegPath = ffmpeg, FrameStaleSeconds = 3, WebRtc = new() { BindAddress = "127.0.0.1" }
        });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options), new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        var blueId = Guid.NewGuid(); var redId = Guid.NewGuid();
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = blue.Url }, CancellationToken.None)) { registration.Publish(blueId); registration.Confirm(); }
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = red.Url }, CancellationToken.None)) { registration.Publish(redId); registration.Confirm(); }
        using var streams = new CameraStreamService(cameras, options);
        await streams.StartAsync(CancellationToken.None);
        var clients = new List<RTCPeerConnection>();
        try
        {
            async Task<(CameraView View, RTCPeerConnection Peer, Task<byte[]> Frame)> Open(Guid id, string owner)
            {
                var peer = new RTCPeerConnection(new RTCConfiguration { X_BindAddress = IPAddress.Loopback });
                clients.Add(peer);
                peer.addTrack(new MediaStreamTrack(new VideoFormat(VideoCodecsEnum.H264, 96, 90000,
                    "packetization-mode=1;profile-level-id=42e01f;level-asymmetry-allowed=1"), MediaStreamStatusEnum.RecvOnly));
                var frame = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                peer.OnVideoFrameReceived += (_, _, data, _) =>
                {
                    if (H264Packetiser.ParseNals(data).Any(n => (n.NAL[0] & 31) == 5)) frame.TrySetResult(data);
                };
                var offer = peer.createOffer(new RTCOfferOptions { X_WaitForIceGatheringToComplete = true });
                await peer.setLocalDescription(offer);
                var view = await streams.OpenAsync(id, owner, new("offer", offer.sdp), CancellationToken.None);
                Assert.DoesNotContain("rtsp:", view.Sdp);
                Assert.DoesNotContain("camera-password", view.Sdp);
                Assert.Equal(SetDescriptionResultEnum.OK, peer.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = view.Sdp }));
                return (view, peer, frame.Task);
            }
            var firstBlue = await Open(blueId, "admin-1");
            var secondBlue = await Open(blueId, "admin-2");
            var firstRed = await Open(redId, "admin-1");
            var blueFrame = await firstBlue.Frame.WaitAsync(TimeSpan.FromSeconds(20));
            var redFrame = await firstRed.Frame.WaitAsync(TimeSpan.FromSeconds(20));
            var bluePixel = await PixelAsync(ffmpeg, blueFrame);
            var redPixel = await PixelAsync(ffmpeg, redFrame);
            Assert.True(bluePixel[2] > bluePixel[0] + 100);
            Assert.True(redPixel[0] > redPixel[2] + 100);
            Assert.Equal(1, blue.AcceptedConnections);
            Assert.Equal(1, red.AcceptedConnections);
            Assert.True((await streams.GetStatusAsync(blueId, firstBlue.View.ViewId, "admin-1", CancellationToken.None)).Transcoding);
            Assert.False((await streams.GetStatusAsync(redId, firstRed.View.ViewId, "admin-1", CancellationToken.None)).Transcoding);
            await Assert.ThrowsAsync<CameraConnectionException>(() => streams.GetStatusAsync(redId, firstBlue.View.ViewId, "admin-1", CancellationToken.None));
            await Assert.ThrowsAsync<CameraConnectionException>(() => streams.GetStatusAsync(blueId, firstBlue.View.ViewId, "admin-2", CancellationToken.None));
            await streams.CloseAsync(blueId, secondBlue.View.ViewId, "admin-1"); // Cannot close another user's peer.
            Assert.Equal("Connected", (await streams.GetStatusAsync(blueId, secondBlue.View.ViewId, "admin-2", CancellationToken.None)).State);
            await streams.CloseAsync(blueId, firstBlue.View.ViewId, "admin-1");
            await Assert.ThrowsAsync<CameraConnectionException>(() => streams.GetStatusAsync(blueId, firstBlue.View.ViewId, "admin-1", CancellationToken.None));
            await secondBlue.Frame.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(RTCPeerConnectionState.connected, secondBlue.Peer.connectionState);
            Assert.NotNull(cameras.GetFrame(blueId));
            var invalid = await Assert.ThrowsAsync<CameraConnectionException>(() => streams.OpenAsync(blueId, "admin-1", new("offer", "invalid SDP"), CancellationToken.None));
            Assert.Equal("InvalidOffer", invalid.Code);
            red.DisconnectClients();
            for (var i = 0; i < 40 && cameras.GetStatus(redId).State == "Connected"; i++) await Task.Delay(100);
            await Assert.ThrowsAsync<CameraConnectionException>(() => streams.GetStatusAsync(redId, firstRed.View.ViewId, "admin-1", CancellationToken.None));
        }
        finally
        {
            foreach (var peer in clients) { peer.Close("Test finished"); peer.Dispose(); }
            await streams.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<byte[]> PixelAsync(string ffmpeg, byte[] video)
    {
        var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-protocol_whitelist", "pipe", "-f", "h264", "-i", "pipe:0", "-frames:v", "1", "-vf", "scale=1:1", "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1" }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        using var output = new MemoryStream();
        var read = process.StandardOutput.BaseStream.CopyToAsync(output);
        await process.StandardInput.BaseStream.WriteAsync(video);
        process.StandardInput.Close();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await read;
        Assert.True(process.ExitCode == 0, await error);
        Assert.Equal(3, output.Length);
        return output.ToArray();
    }
}
