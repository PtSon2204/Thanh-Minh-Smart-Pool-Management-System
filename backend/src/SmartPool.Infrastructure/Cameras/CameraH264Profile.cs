using SIPSorcery.Net;

namespace SmartPool.Infrastructure.Cameras;

internal static class CameraH264Profile
{
    public static bool CanPassthrough(byte[] frame) => H264Packetiser.ParseNals(frame).Any(n => n.NAL.Length >= 4
        && (n.NAL[0] & 31) == 7 && n.NAL[1] == 66 && (n.NAL[2] & 0x40) != 0 && n.NAL[3] <= 31);
}
