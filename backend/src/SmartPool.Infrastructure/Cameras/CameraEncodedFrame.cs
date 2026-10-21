using System.Net;
using System.Threading.Channels;
using SharpRTSPClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

internal sealed record CameraEncodedFrame(byte[] Data, string Format, bool IsKeyFrame, uint RtpTimestamp);
