namespace SmartPool.Infrastructure.Cameras;

// One continuous 90 kHz capture clock, including publisher reconnects and uint wrap.
internal sealed class CameraRtpClock
{
    private uint? previous;//PTP timestamp của khung hình trước đó, được sử dụng để tính toán sự khác biệt giữa các khung hình liên tiếp và duy trì đồng hồ RTP liên tục.
    private uint current;//RTP timestamp hiện tại, được cập nhật mỗi khi một khung hình mới được quan sát và được sử dụng để đồng bộ hóa các khung hình trong luồng RTP.
    private DateTimeOffset? observedAt;
    public void NewSource() => previous = null;
    public uint Observe(uint source, DateTimeOffset observed)//source: PTP timestamp gốc từ camera, observed: thời điểm thực tế nhận,quan sát khung hình mới.
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
