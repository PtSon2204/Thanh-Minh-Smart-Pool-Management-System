namespace SmartPool.Infrastructure.Cameras;

internal static class JpegFrames
{
    public static async Task ReadAsync(Stream stream, Action<byte[]> receive, CancellationToken token)
    {
        var buffer = new byte[16384];
        using var frame = new MemoryStream();
        var previous = -1;
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0)
        {
            for (var i = 0; i < count; i++)
            {
                var value = buffer[i];
                if (frame.Length == 0)
                {
                    if (previous == 0xff && value == 0xd8) { frame.WriteByte(0xff); frame.WriteByte(value); }
                }
                else
                {
                    frame.WriteByte(value);
                    if (frame.Length > 4 * 1024 * 1024) throw new IOException("JPEG frame exceeds limit.");
                    if (previous == 0xff && value == 0xd9) { receive(frame.ToArray()); frame.SetLength(0); }
                }
                previous = value;
            }
        }
    }
}
