using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

/// <summary>
/// Áp ngưỡng giám sát an toàn lưu trong DB lên instance <see cref="RiskOptions"/>
/// dùng chung (singleton qua <c>IOptions&lt;RiskOptions&gt;</c>) ngay khi khởi động,
/// để engine LIVE và video tải lên cùng chạy đúng bộ ngưỡng đã chỉnh cuối.
/// </summary>
public sealed class SafetyThresholdsBootstrap(IRiskThresholds thresholds, ILogger<SafetyThresholdsBootstrap> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await thresholds.InitializeAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Không áp dụng được ngưỡng từ DB khi khởi động; dùng appsettings.");
        }
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await thresholds.InitializeAsync(stoppingToken);
    }
}
