using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

public sealed record CameraTarget(Uri Uri, string Identity);

public sealed class CameraAddressPolicy(IOptions<CameraOptions> options)
{
    public async Task<CameraTarget> ValidateAsync(CameraConnectionConfiguration configuration, CancellationToken cancellationToken)
    {
        if (configuration.Protocol != "RTSP" || !Uri.TryCreate(configuration.StreamUrl, UriKind.Absolute, out var uri) || uri.Scheme != "rtsp" ||
            uri.Port == 0 || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) || configuration.StreamUrl.Any(char.IsControl))
            throw new CameraConnectionException("InvalidConfiguration", "Cấu hình RTSP không hợp lệ. Không nhúng thông tin đăng nhập vào URL.");
        var port = uri.Port > 0 ? uri.Port : 554;
        var endpoint = options.Value.AllowedEndpoints.FirstOrDefault(x => x.Port == port && string.Equals(x.Host, uri.DnsSafeHost, StringComparison.OrdinalIgnoreCase));
        if (endpoint == null)
            throw new CameraConnectionException("AddressNotAllowed", "Địa chỉ hoặc cổng camera không nằm trong phạm vi được phép kết nối.");
        IPAddress address;
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literal)) address = literal;
        else
        {
            var approved = endpoint.Addresses.Select(x => IPAddress.TryParse(x, out var ip) ? ip : null).OfType<IPAddress>().ToArray();
            if (approved.Length == 0) throw new CameraConnectionException("AddressNotAllowed", "Hostname camera chưa có địa chỉ IP được cho phép.");
            var resolved = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            if (resolved.Length == 0 || resolved.Any(x => !approved.Contains(x)))
                throw new CameraConnectionException("AddressNotAllowed", "Hostname camera phân giải ra địa chỉ chưa được cho phép.");
            address = resolved.OrderBy(x => x.ToString(), StringComparer.Ordinal).First();
        }
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.IsIPv6Multicast || address.IsIPv6LinkLocal ||
            (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (address.GetAddressBytes()[0] >= 224 || address.ToString().StartsWith("169.254.", StringComparison.Ordinal))) || address.ScopeIdSafe() != 0)
            throw new CameraConnectionException("AddressNotAllowed", "Địa chỉ camera không hợp lệ.");
        var pinned = new UriBuilder(uri) { Host = address.ToString(), Port = port }.Uri;
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{address}:{port}")));
        return new CameraTarget(pinned, identity);
    }
}

internal static class CameraAddressExtensions
{
    public static long ScopeIdSafe(this IPAddress address) => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? address.ScopeId : 0;
}
