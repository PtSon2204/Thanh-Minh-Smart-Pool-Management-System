using SmartPool.Application.Features.ManageEquipments;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraFrameDecoder(IOptions<CameraOptions> options) : ICameraFrameDecoder
{
    public async Task<byte[]?> DecodeAsync(byte[] encodedFrame, string format, CancellationToken cancellationToken)
    {
        if (format is not ("h264" or "hevc" or "mjpeg") || encodedFrame.Length > 4 * 1024 * 1024) return null;
        if (string.IsNullOrWhiteSpace(options.Value.FfmpegPath) || !Path.IsPathFullyQualified(options.Value.FfmpegPath) || !File.Exists(options.Value.FfmpegPath))
            throw new CameraConnectionException("NotConfigured", "Chưa cấu hình đường dẫn FFmpeg cho camera.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var start = new ProcessStartInfo(options.Value.FfmpegPath)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        // Decoder sees encoded bytes only, never a URL, password or network protocol.
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-protocol_whitelist", "pipe", "-f", format, "-i", "pipe:0", "-frames:v", "1", "-vf", "scale=640:-2", "-f", "image2pipe", "-vcodec", "mjpeg", "pipe:1" }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new CameraConnectionException("NotConfigured", "Không thể khởi động bộ giải mã camera.");
        using var cancellation = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var outputTask = ReadBoundedAsync(process.StandardOutput.BaseStream, 4 * 1024 * 1024, timeout.Token);
        var errorTask = DrainAsync(process.StandardError.BaseStream, timeout.Token);
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(encodedFrame, timeout.Token);
            process.StandardInput.Close();
            var output = await outputTask;
            await process.WaitForExitAsync(timeout.Token);
            await errorTask;
            return process.ExitCode == 0 && output.Length > 4 && output[0] == 0xff && output[1] == 0xd8 && output[^2] == 0xff && output[^1] == 0xd9 ? output : null;
        }
        finally
        {
            if (!process.HasExited) process.Kill(true);
            try { await Task.WhenAll(outputTask, errorTask); } catch (Exception) { /* Observe tasks, never log decoder input or output. */ }
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken token)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + read > limit) throw new InvalidDataException("Camera frame exceeds decoder limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static async Task DrainAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[4096];
        while (await stream.ReadAsync(buffer, token) > 0) { }
    }
}
