using System.Reflection;
using SmartPool.Infrastructure.Cameras;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class CameraJpegParserTests
{
    private static Task Read(Stream stream, Action<byte[]> emit) => (Task)typeof(CameraService).Assembly
        .GetType("SmartPool.Infrastructure.Cameras.CameraAnalysisFramePump")!
        .GetMethod("ReadJpegsAsync", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [stream, emit, CancellationToken.None])!;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(17)]
    [InlineData(16384)]
    public async Task MarkerBoundariesAndMultipleFrames_PreserveEveryJpegByte(int chunk)
    {
        byte[] first = [0xff, 0xd8, 1, 0xff, 0, 2, 0xff, 0xd9];
        byte[] large = [0xff, 0xd8, ..Enumerable.Repeat((byte)37, 50000), 0xff, 0xd9];
        byte[] last = [0xff, 0xd8, 0xff, 0xd8, 3, 0xff, 0xd9];
        using var input = new ChunkedStream([9, 0xff, 0, ..first, 10, ..large, ..last, 0xff, 0xd8, 1], chunk);
        var frames = new List<byte[]>();
        await Read(input, frames.Add);
        Assert.Equal(3, frames.Count);
        Assert.Equal(first, frames[0]); Assert.Equal(large, frames[1]); Assert.Equal(last, frames[2]);
    }

    [Fact]
    public async Task OversizedJpeg_IsRejectedWithoutPublishingPartialPixels()
    {
        // EOI is split exactly at the 4 MiB boundary; the final byte must still be counted.
        using var input = new MemoryStream([0xff, 0xd8, ..new byte[4 * 1024 * 1024 - 3], 0xff, 0xd9]);
        var emitted = false;
        await Assert.ThrowsAsync<IOException>(() => Read(input, _ => emitted = true));
        Assert.False(emitted);
    }

    private sealed class ChunkedStream(byte[] data, int chunk) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, chunk)], ct);
    }
}
