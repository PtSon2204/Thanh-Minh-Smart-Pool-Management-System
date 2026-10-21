using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

public sealed record CameraTarget(Uri Uri, string Identity);
