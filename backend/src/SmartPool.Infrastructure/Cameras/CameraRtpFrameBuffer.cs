namespace SmartPool.Infrastructure.Cameras;

internal sealed record BufferedRtpFrame(uint Timestamp, DateTimeOffset CapturedAt, ReadOnlyMemory<byte>[] Parts);

// A picture may arrive as several callbacks/slices sharing one RTP timestamp.
// Close the access unit only when the next timestamp arrives (one frame of latency).
internal sealed class CameraRtpFrameBuffer
{
    private uint? timestamp;
    private DateTimeOffset capturedAt;
    private readonly List<ReadOnlyMemory<byte>> parts = [];
    private int length;
    private bool discarded;

    public BufferedRtpFrame? Push(uint next, DateTimeOffset observed, IEnumerable<ReadOnlyMemory<byte>> data)
    {
        BufferedRtpFrame? complete = null;
        if (timestamp.HasValue && timestamp != next)
        {
            if (!discarded && parts.Count > 0) complete = new(timestamp.Value, capturedAt, parts.ToArray());
            parts.Clear(); length = 0; discarded = false; timestamp = null;
        }
        if (!timestamp.HasValue) { timestamp = next; capturedAt = observed; }
        if (discarded) return complete;
        foreach (var part in data)
        {
            if (part.IsEmpty) continue;
            if (part.Length > 4 * 1024 * 1024 - length || parts.Count >= 1024)
            { parts.Clear(); discarded = true; break; }
            // The RTSP library owns pooled callback buffers; keep our own immutable bytes.
            parts.Add(part.ToArray()); length += part.Length;
        }
        return complete;
    }
}
