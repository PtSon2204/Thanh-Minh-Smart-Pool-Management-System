namespace SmartPool.Infrastructure.Cameras;

// One continuous 90 kHz capture clock, including publisher reconnects and uint wrap.
internal sealed class CameraRtpClock
{
    private uint? previous;
    private uint current;
    private DateTimeOffset? observedAt;
    public void NewSource() => previous = null;
    public uint Observe(uint source, DateTimeOffset observed)
    {
        if (!observedAt.HasValue) current = source;
        else
        {
            var delta = previous.HasValue ? unchecked(source - previous.Value) : uint.MaxValue;
            if (delta == 0 || delta > 180000)
                delta = (uint)Math.Clamp((observed - observedAt.Value).TotalSeconds * 90000, 750, 900000);
            current = unchecked(current + delta);
        }
        previous = source; observedAt = observed;
        return current;
    }
}
