using SmartPool.Infrastructure.Cameras;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class CameraRtpFrameBufferTests
{
    private static readonly Type Type = typeof(CameraService).Assembly.GetType("SmartPool.Infrastructure.Cameras.CameraRtpFrameBuffer")!;

    [Fact]
    public void SharedTimestampSlices_FormOneImmutablePicture_WithOriginalCaptureTime()
    {
        var buffer = Activator.CreateInstance(Type, nonPublic: true)!;
        var time = DateTimeOffset.UtcNow;
        object? Push(uint timestamp, DateTimeOffset observed, params byte[][] parts) => Type.GetMethod("Push")!.Invoke(buffer,
            [timestamp, observed, parts.Select(part => new ReadOnlyMemory<byte>(part))]);
        byte[] pooled = [0x67, 1, 2];
        Assert.Null(Push(100, time, pooled));
        pooled[0] = 0;
        Assert.Null(Push(100, time.AddMilliseconds(1), [0x68, 1]));
        Assert.Null(Push(100, time.AddMilliseconds(2), [0x65, 1]));
        Assert.Null(Push(100, time.AddMilliseconds(3), [0x65, 2]));
        var picture = Push(200, time.AddMilliseconds(40), [0x41, 1])!;
        Assert.Equal(100u, picture.GetType().GetProperty("Timestamp")!.GetValue(picture));
        Assert.Equal(time, picture.GetType().GetProperty("CapturedAt")!.GetValue(picture));
        var data = (ReadOnlyMemory<byte>[])picture.GetType().GetProperty("Parts")!.GetValue(picture)!;
        Assert.Equal(4, data.Length); Assert.Equal(0x67, data[0].Span[0]);
        var next = Push(300, time.AddMilliseconds(80), [0x41, 2])!;
        Assert.Single((ReadOnlyMemory<byte>[])next.GetType().GetProperty("Parts")!.GetValue(next)!);
    }

    [Fact]
    public void OversizedPicture_IsDroppedAsAWhole_ThenNextPictureCanRecover()
    {
        var buffer = Activator.CreateInstance(Type, nonPublic: true)!;
        object? Push(uint timestamp, byte[] bytes) => Type.GetMethod("Push")!.Invoke(buffer,
            [timestamp, DateTimeOffset.UtcNow, new[] { new ReadOnlyMemory<byte>(bytes) }]);
        Assert.Null(Push(1, new byte[4 * 1024 * 1024 + 1]));
        Assert.Null(Push(1, [0x65, 1]));
        Assert.Null(Push(2, [0x65, 2]));
        Assert.NotNull(Push(3, [0x41, 1]));
    }
}
