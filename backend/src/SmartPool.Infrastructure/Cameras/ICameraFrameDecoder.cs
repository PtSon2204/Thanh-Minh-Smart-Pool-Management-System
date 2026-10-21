using System.Diagnostics;
using Microsoft.Extensions.Options;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

public interface ICameraFrameDecoder
{
    Task<byte[]?> DecodeAsync(byte[] encodedFrame, string format, CancellationToken cancellationToken);
}
