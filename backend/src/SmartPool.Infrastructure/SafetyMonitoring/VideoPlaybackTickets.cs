using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class VideoPlaybackTickets(IDataProtectionProvider protection, TimeProvider time) : IVideoPlaybackTickets
{
    private readonly IDataProtector protector = protection.CreateProtector("SmartPool.VideoPlayback.v1");
    public VideoPlaybackTicket Create(Guid jobId)
    {
        var expires = time.GetUtcNow().AddMinutes(15);
        return new(protector.Protect($"{jobId:N}:{expires.ToUnixTimeSeconds()}"), expires);
    }
    public bool Validate(Guid jobId, string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 4096) return false;
        try
        {
            var parts = protector.Unprotect(ticket).Split(':');
            return parts.Length == 2 && parts[0] == jobId.ToString("N") &&
                long.TryParse(parts[1], out var expires) && expires > time.GetUtcNow().ToUnixTimeSeconds();
        }
        catch (Exception error) when (error is CryptographicException or FormatException) { return false; }
    }
}
