using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class MonitoringFiles(IOptions<MonitoringOptions> options, IHostEnvironment environment)
{
    public string Root { get; } = Path.GetFullPath(string.IsNullOrWhiteSpace(options.Value.StoragePath)
        ? Path.Combine(environment.ContentRootPath, "App_Data", "safety-monitoring") : options.Value.StoragePath);

    public string PathFor(string reference)
    {
        if (reference != Path.GetFileName(reference) || reference.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || reference is "." or ".." ||
            string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Invalid file reference.");
        Directory.CreateDirectory(Root);
        var path = Path.GetFullPath(Path.Combine(Root, reference));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Invalid file reference.");
        return path;
    }
}
