using System.Reflection;
using SmartPool.Infrastructure.Cameras;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class CameraAccessUnitTests
{
    [Theory]
    [InlineData("H264", 7, 8, 5)]
    [InlineData("H265", 32, 33, 19)]
    public void ParameterOnlyPackets_DoNotBecomeVideoFrames_ButRemainAvailableForNextKeyframe(string codec, int first, int second, int picture)
    {
        var type = typeof(CameraService).Assembly.GetType("SmartPool.Infrastructure.Cameras.CameraKeyFrame")!;
        var assembler = Activator.CreateInstance(type, nonPublic: true)!;
        type.GetMethod("Configure")!.Invoke(assembler, [codec, null]);
        byte[] Nal(int kind) => [(byte)(codec == "H265" ? kind << 1 : kind), 1, 2, 3];
        object? Read(params byte[][] nals) => type.GetMethod("Read")!.Invoke(assembler,
            [nals.Select(nal => new ReadOnlyMemory<byte>(nal)), true, false]);
        var firstNal = Nal(first); var secondNal = Nal(second);
        Assert.Null(Read(firstNal)); Assert.Null(Read(secondNal));
        if (codec == "H264") { Assert.Null(Read(Nal(9))); Assert.Null(Read(Nal(6))); }
        var frame = ((byte[] Data, string Format, bool Key))Read(Nal(picture))!;
        Assert.True(frame.Key);
        Assert.Equal(codec == "H265" ? "hevc" : "h264", frame.Format);
        byte[] expected = [0, 0, 0, 1, ..firstNal, 0, 0, 0, 1, ..secondNal, 0, 0, 0, 1, ..Nal(picture)];
        Assert.Equal(expected, frame.Data);
        var delta = ((byte[] Data, string Format, bool Key))Read(Nal(1))!;
        Assert.False(delta.Key);
    }
}
