using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class SafetyMonitoringThresholdConfiguration : IEntityTypeConfiguration<SafetyMonitoringThreshold>
{
    public void Configure(EntityTypeBuilder<SafetyMonitoringThreshold> entity)
    {
        entity.ToTable("safety_monitoring_thresholds", "smart_pool");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ConfidenceThreshold).HasColumnName("confidence_threshold");
        entity.Property(e => e.WarningRatio).HasColumnName("warning_ratio");
        entity.Property(e => e.CriticalRatio).HasColumnName("critical_ratio");
        entity.Property(e => e.RecoveryRatio).HasColumnName("recovery_ratio");
        entity.Property(e => e.CriticalHoldSeconds).HasColumnName("critical_hold_seconds");
        entity.Property(e => e.RecoveryHoldSeconds).HasColumnName("recovery_hold_seconds");
        entity.Property(e => e.CooldownSeconds).HasColumnName("cooldown_seconds");
        entity.Property(e => e.WindowSeconds).HasColumnName("window_seconds");
        entity.Property(e => e.MaxObservationGapSeconds).HasColumnName("max_observation_gap_seconds");
        entity.Property(e => e.MinObservations).HasColumnName("min_observations");
        entity.Property(e => e.Version).HasColumnName("version").HasDefaultValue(0);
        entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        // Bảng single-row: seed đúng một bản ghi giá trị mặc định (đồng bộ RiskOptions).
        entity.HasData(new SafetyMonitoringThreshold
        {
            Id = SafetyMonitoringThreshold.SingletonId,
            ConfidenceThreshold = 0.60,
            WarningRatio = 0.40,
            CriticalRatio = 0.70,
            RecoveryRatio = 0.20,
            CriticalHoldSeconds = 3,
            RecoveryHoldSeconds = 5,
            CooldownSeconds = 15,
            WindowSeconds = 5,
            MaxObservationGapSeconds = 3,
            MinObservations = 3,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedBy = null,
        });
    }
}
