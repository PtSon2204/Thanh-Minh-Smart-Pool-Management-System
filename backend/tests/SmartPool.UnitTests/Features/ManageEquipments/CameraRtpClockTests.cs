using SmartPool.Infrastructure.Cameras;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class CameraRtpClockTests
{
    [Fact]
    public void PreservesNormalSpacingAndWrap_AndUsesElapsedTimeForANewPublisher()
    {
        var type = typeof(CameraService).Assembly.GetType("SmartPool.Infrastructure.Cameras.CameraRtpClock")!;
        var clock = Activator.CreateInstance(type, nonPublic: true)!;
        var time = DateTimeOffset.UtcNow;
        uint Observe(uint timestamp, int milliseconds) => (uint)type.GetMethod("Observe")!.Invoke(clock, [timestamp, time.AddMilliseconds(milliseconds)])!;
        Assert.Equal(0xfffff000u, Observe(0xfffff000u, 0));
        Assert.Equal(0x1000u, Observe(0x1000u, 91));
        Assert.Equal(0x1000u + 3600, Observe(0x1000u + 3600, 131));
        type.GetMethod("NewSource")!.Invoke(clock, null);
        Assert.Equal(0x1000u + 3600 + 18000, Observe(789000u, 331));
        Assert.Equal(0x1000u + 7200 + 18000, Observe(792600u, 371));
    }
}
