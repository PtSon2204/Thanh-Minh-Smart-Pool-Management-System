using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

internal static class CameraAddressExtensions
{
    public static long ScopeIdSafe(this IPAddress address) => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? address.ScopeId : 0;
}
