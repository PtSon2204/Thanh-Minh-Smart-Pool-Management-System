using SharpRTSPClient;

namespace SmartPool.Infrastructure.Cameras;

// Parameter sets may arrive in SDP, in RTP, or both. Copy callback data before the
// library returns its pooled buffers. FFmpeg receives one independent Annex B frame.
internal sealed class CameraKeyFrame
{
    private const int Limit = 4 * 1024 * 1024;
    private readonly Dictionary<int, byte[]> parameters = [];
    private string? format;

    public void Configure(string codec, IStreamConfigurationData? configuration)
    {
        format = codec.ToUpperInvariant() switch { "H264" => "h264", "H265" => "hevc", "JPEG" => "mjpeg", _ => null };
        parameters.Clear();
        if (configuration is H264StreamConfigurationData h264) { Set(7, h264.SPS); Set(8, h264.PPS); }
        if (configuration is H265StreamConfigurationData h265) { Set(32, h265.VPS); Set(33, h265.SPS); Set(34, h265.PPS); }
    }

    public (byte[] Data, string Format, bool IsKeyFrame)? Read(IEnumerable<ReadOnlyMemory<byte>> segments, bool capture, bool keyFramesOnly = true)
    {
        if (format == null) return null;
        var parts = new List<ReadOnlyMemory<byte>>();
        var length = 0;
        var keyFrame = format == "mjpeg";
        foreach (var segment in segments)
        {
            var part = format == "mjpeg" ? segment : StripStartCode(segment);
            if (part.IsEmpty) continue;
            length += part.Length + 4;
            if (length > Limit) return null;
            var type = format == "hevc" ? (part.Span[0] >> 1) & 63 : part.Span[0] & 31;
            if (format == "hevc" && type is 32 or 33 or 34 || format == "h264" && type is 7 or 8)
                Set(type, part.ToArray());
            keyFrame |= format == "hevc" ? type is >= 16 and <= 21 : format == "h264" && type == 5;
            parts.Add(part);
        }
        if (!capture || (keyFramesOnly && !keyFrame) || parts.Count == 0) return null;
        var prefix = format == "mjpeg" || !keyFrame ? [] : parameters.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
        length += prefix.Sum(x => x.Length + 4);
        if (length > Limit) return null;
        using var output = new MemoryStream(length);
        foreach (var part in prefix) { output.Write(new byte[] { 0, 0, 0, 1 }); output.Write(part); }
        foreach (var part in parts)
        {
            if (format != "mjpeg") output.Write(new byte[] { 0, 0, 0, 1 });
            output.Write(part.Span);
        }
        return (output.ToArray(), format, keyFrame);
    }

    private void Set(int type, byte[]? data)
    {
        if (data is { Length: > 0 and <= 65536 }) parameters[type] = data.ToArray();
    }

    private static ReadOnlyMemory<byte> StripStartCode(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        if (span.Length >= 4 && span[0] == 0 && span[1] == 0 && span[2] == 0 && span[3] == 1) return data[4..];
        if (span.Length >= 3 && span[0] == 0 && span[1] == 0 && span[2] == 1) return data[3..];
        return data;
    }
}
