using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.SafetyMonitoring;

/// <summary>
/// Singleton nguồn ngưỡng AI: giữ instance <see cref="RiskOptions"/> đang hiệu lực
/// (chia sẻ với mọi engine qua <c>IOptions&lt;RiskOptions&gt;</c>) và áp bảng DB lên nó.
/// Lưu ngưỡng mới = mutate instance hiện tại (không thay engine, không reset state
/// theo dõi: episode, cửa sổ, cooldown) — đổi ngưỡng hiệu lực ngay ở khung hình kế tiếp.
/// </summary>
public sealed class RiskThresholds(IServiceScopeFactory scopes, IOptions<RiskOptions> configured,
    ILogger<RiskThresholds> logger) : IRiskThresholds
{
    private readonly RiskOptions _current = configured.Value;
    private readonly SemaphoreSlim gate = new(1, 1);
    private int appliedVersion;

    public RiskOptions Current => _current;

    public Task InitializeAsync(CancellationToken ct) => ApplyDatabaseAsync(ct);

    public async Task<SafetyThresholdsDto> GetEffectiveAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
        var row = await db.SafetyMonitoringThresholds.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == SafetyMonitoringThreshold.SingletonId, ct);
        if (row is null) return _current.ToDto(0, "APPSETTINGS");
        return new(row.ConfidenceThreshold, row.WarningRatio, row.CriticalRatio, row.RecoveryRatio,
            row.CriticalHoldSeconds, row.RecoveryHoldSeconds, row.CooldownSeconds, row.WindowSeconds,
            row.MaxObservationGapSeconds, row.MinObservations, row.Version, "DATABASE", row.UpdatedBy, row.UpdatedAt);
    }

    public async Task<SafetyThresholdsDto> SaveAsync(SafetyThresholdsDto input, Guid updatedBy, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var options = new RiskOptions
            {
                ConfidenceThreshold = input.ConfidenceThreshold,
                WarningRatio = input.WarningRatio,
                CriticalRatio = input.CriticalRatio,
                RecoveryRatio = input.RecoveryRatio,
                CriticalHoldSeconds = input.CriticalHoldSeconds,
                RecoveryHoldSeconds = input.RecoveryHoldSeconds,
                CooldownSeconds = input.CooldownSeconds,
                WindowSeconds = input.WindowSeconds,
                MaxObservationGapSeconds = input.MaxObservationGapSeconds,
                MinObservations = input.MinObservations,
            };
            if (!options.IsValid()) throw new ArgumentException("Bộ ngưỡng không hợp lệ.");

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Đồng bộ version/upsert cả khi nhiều API instance lưu đồng thời.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(782341250014)", ct);
            var row = await db.SafetyMonitoringThresholds
                .FirstOrDefaultAsync(x => x.Id == SafetyMonitoringThreshold.SingletonId, ct);
            var version = row is null ? 1 : row.Version + 1;
            if (row is null)
            {
                row = new SafetyMonitoringThreshold { Id = SafetyMonitoringThreshold.SingletonId, CreatedAt = DateTime.UtcNow };
                db.SafetyMonitoringThresholds.Add(row);
            }
            row.ConfidenceThreshold = options.ConfidenceThreshold;
            row.WarningRatio = options.WarningRatio;
            row.CriticalRatio = options.CriticalRatio;
            row.RecoveryRatio = options.RecoveryRatio;
            row.CriticalHoldSeconds = options.CriticalHoldSeconds;
            row.RecoveryHoldSeconds = options.RecoveryHoldSeconds;
            row.CooldownSeconds = options.CooldownSeconds;
            row.WindowSeconds = options.WindowSeconds;
            row.MaxObservationGapSeconds = options.MaxObservationGapSeconds;
            row.MinObservations = options.MinObservations;
            row.Version = version;
            row.UpdatedBy = updatedBy;
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Áp nóng SAU khi đã lưu DB: đổi ngưỡng chỉ có hiệu lực khi persist thành công.
            _current.CopyFrom(options);
            appliedVersion = version;
            logger.LogInformation("Đã lưu và áp dụng ngưỡng giám sát an toàn version {Version} bởi {UpdatedBy}.", version, updatedBy);
            return options.ToDto(version, "DATABASE", updatedBy, row.UpdatedAt);
        }
        finally { gate.Release(); }
    }

    private static RiskOptions FromRow(SafetyMonitoringThreshold row) => new()
    {
        ConfidenceThreshold = row.ConfidenceThreshold,
        WarningRatio = row.WarningRatio,
        CriticalRatio = row.CriticalRatio,
        RecoveryRatio = row.RecoveryRatio,
        CriticalHoldSeconds = row.CriticalHoldSeconds,
        RecoveryHoldSeconds = row.RecoveryHoldSeconds,
        CooldownSeconds = row.CooldownSeconds,
        WindowSeconds = row.WindowSeconds,
        MaxObservationGapSeconds = row.MaxObservationGapSeconds,
        MinObservations = row.MinObservations,
    };

    private async Task ApplyDatabaseAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
            var row = await db.SafetyMonitoringThresholds.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == SafetyMonitoringThreshold.SingletonId, ct);
            if (row is null || row.Version <= appliedVersion) return;
            var next = FromRow(row);
            if (!next.IsValid()) throw new ArgumentException("Ngưỡng giám sát trong DB không hợp lệ.");
            _current.CopyFrom(next);
            appliedVersion = row.Version;
            logger.LogInformation("Đã áp dụng ngưỡng giám sát an toàn từ DB (version {Version}).", row.Version);
        }
        catch (Exception ex)
        {
            // DB chưa có bảng (chưa migration) hoặc không khả dụng: chạy tiếp bằng appsettings, không chặn khởi động.
            logger.LogWarning(ex, "Không áp dụng được ngưỡng từ DB, dùng giá trị appsettings.");
        }
        finally { gate.Release(); }
    }
}
