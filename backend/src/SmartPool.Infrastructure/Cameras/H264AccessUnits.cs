using System.Diagnostics;
using System.Threading.Channels;
using SIPSorcery.Net;

namespace SmartPool.Infrastructure.Cameras;

// FFmpeg emits AUD-delimited Annex B access units. Keep at most one bounded frame,
// and emit when the next AUD arrives (one frame of delay, no HLS segment buffer).
internal static class H264AccessUnits
{
    public static async Task ReadAsync(Stream stream, Action<byte[]> emit, CancellationToken token)
    {
        var buffer = new byte[16384];
        using var nal = new MemoryStream();
        using var frame = new MemoryStream();
        var zeros = 0;
        var hasVideo = false;
        void FlushNal()
        {
            if (nal.Length == 0) return;
            var type = nal.GetBuffer()[0] & 31;
            if (type == 9 && hasVideo)
            {
                emit(frame.ToArray());
                frame.SetLength(0);
                hasVideo = false;
            }
            frame.Write(new byte[] { 0, 0, 0, 1 });
            frame.Write(nal.GetBuffer(), 0, (int)nal.Length);
            if (frame.Length > 4 * 1024 * 1024) throw new IOException("Video frame exceeds limit.");
            hasVideo |= type is 1 or 5;
            nal.SetLength(0);
        }
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0)
        {
            for (var i = 0; i < count; i++)
            {
                var value = buffer[i];
                if (value == 0) { if (++zeros > 4 * 1024 * 1024) throw new IOException("Video NAL exceeds limit."); continue; }
                if (value == 1 && zeros >= 2) { FlushNal(); zeros = 0; continue; }
                while (zeros > 0) { nal.WriteByte(0); zeros--; }
                nal.WriteByte(value);
                if (nal.Length > 4 * 1024 * 1024) throw new IOException("Video NAL exceeds limit.");
            }
        }
        FlushNal();
        if (hasVideo) emit(frame.ToArray());
    }
}
