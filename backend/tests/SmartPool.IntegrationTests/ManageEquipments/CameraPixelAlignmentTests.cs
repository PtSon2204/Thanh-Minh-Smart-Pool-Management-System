using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.IntegrationTests.ManageEquipments;

public sealed class CameraPixelAlignmentTests
{
    [CameraTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecodedPixelIdentity_MatchesRtp_EvenAfterCorruptedPicture(bool corrupt)
    {
        var ffmpeg = CameraTestEnvironment.GetRequiredFfmpeg();
        await using var rtsp = await LocalRtspCamera.StartNumberedAsync(ffmpeg);
        var options = Options.Create(new CameraOptions { FfmpegPath = ffmpeg,
            AllowedEndpoints = [new() { Host = "127.0.0.1", Port = rtsp.Port }] });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options),
            new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        var id = Guid.NewGuid();
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = rtsp.Url }, default))
        { registration.Publish(id); registration.Confirm(); }
        await using var source = new CameraFrameSource(cameras, options);
        rtsp.CorruptNumberedFrames = corrupt;
        var samples = new List<CameraCapturedFrame>();
        var seen = new HashSet<string>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(7);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var sample = source.Capture(id);
            if (sample != null && seen.Add(sample.FrameId)) samples.Add(sample);
            await Task.Delay(20);
        }
        Assert.True(samples.Count >= 10, $"Only {samples.Count} numbered images decoded.");
        foreach (var sample in samples.Where((_, index) => index % 3 == 0))
        {
            var measured = await MeanLuminanceAsync(ffmpeg, sample.Jpeg);
            // JPEG conversion expands limited-range Y to full range. Twelve Y
            // levels separate IDs by ~14 intensity values; tolerance < half.
            var pixelId = (int)Math.Round(measured * 219 / 255 / 12);
            var rtpId = (int)(sample.RtpTimestamp!.Value / 9000 % 16);
            Assert.Equal(rtpId, pixelId);
        }
    }

    private static async Task<double> MeanLuminanceAsync(string ffmpeg, byte[] jpeg)
    {
        var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-f", "image2pipe", "-i", "pipe:0", "-frames:v", "1", "-vf", "scale=1:1",
            "-pix_fmt", "gray", "-f", "rawvideo", "pipe:1" }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancel = deadline.Token.Register(() => { try { process.Kill(true); } catch { } });
        var errors = process.StandardError.ReadToEndAsync();
        using var output = new MemoryStream();
        var reading = process.StandardOutput.BaseStream.CopyToAsync(output, deadline.Token);
        await process.StandardInput.BaseStream.WriteAsync(jpeg, deadline.Token);
        process.StandardInput.Close();
        await process.WaitForExitAsync(deadline.Token); await reading;
        Assert.True(process.ExitCode == 0, await errors);
        return Assert.Single(output.ToArray());
    }
}
