using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

/// <summary>
/// Áp ngưỡng giám sát an toàn lưu trong DB lên instance <see cref="RiskOptions"/>
/// dùng chung (singleton qua <c>IOptions&lt;RiskOptions&gt;</c>) ngay khi khởi động,
/// để engine LIVE và video tải lên cùng chạy đúng bộ ngưỡng đã chỉnh cuối.
/// </summary>
public sealed class SafetyThresholdsBootstrap(IRiskThresholds thresholds, ILogger<SafetyThresholdsBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await thresholds.InitializeAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Không áp dụng được ngưỡng từ DB khi khởi động; dùng appsettings.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
