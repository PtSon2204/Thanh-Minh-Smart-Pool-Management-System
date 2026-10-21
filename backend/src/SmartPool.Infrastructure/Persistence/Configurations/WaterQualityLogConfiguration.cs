using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class WaterQualityLogConfiguration : IEntityTypeConfiguration<WaterQualityLog>
{
    public void Configure(EntityTypeBuilder<WaterQualityLog> entity)
    {
        entity.ToTable("water_quality_logs", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_water_quality_logs_source", "source IN ('Manual', 'Device')");
            table.HasCheckConstraint("ck_water_quality_logs_status", "status IN ('NotEvaluated', 'Incomplete', 'WithinLimits', 'Warning', 'Danger')");
            table.HasCheckConstraint("ck_water_quality_logs_ph_nonnegative", "ph_value >= 0");
            table.HasCheckConstraint("ck_water_quality_logs_chlorine_nonnegative", "free_chlorine_mg_l >= 0");
            table.HasCheckConstraint("ck_water_quality_logs_source_identity", """
                    (source = 'Manual' AND inspected_by IS NOT NULL AND source_event_id IS NULL)
                    OR
                    (source = 'Device' AND inspected_by IS NULL AND equipment_id IS NOT NULL
                        AND source_event_id IS NOT NULL AND length(btrim(source_event_id)) > 0)
                    """);
        });
        entity.HasKey(e => e.Id).HasName("water_quality_logs_pkey");
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.InspectionTime).HasColumnName("inspection_time").HasColumnType("timestamp with time zone");
        entity.Property(e => e.PhValue).HasColumnName("ph_value").HasPrecision(4, 2);
        entity.Property(e => e.FreeChlorineMgL).HasColumnName("free_chlorine_mg_l").HasPrecision(5, 2);
        entity.Property(e => e.IsWaterClear).HasColumnName("is_water_clear");
        entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(20).IsRequired().HasDefaultValue("Manual");
        entity.Property(e => e.InspectedBy).HasColumnName("inspected_by");
        entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
        entity.Property(e => e.SourceEventId).HasColumnName("source_event_id").HasMaxLength(128);
        entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("NotEvaluated");
        entity.Property(e => e.Recommendation).HasColumnName("recommendation").HasMaxLength(500);
        entity.Property(e => e.EvaluationVersion).HasColumnName("evaluation_version").HasMaxLength(100);
        entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.HasOne(e => e.Inspector).WithMany().HasForeignKey(e => e.InspectedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("water_quality_logs_inspected_by_fkey");
        entity.HasOne(e => e.Equipment).WithMany().HasForeignKey(e => e.EquipmentId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("water_quality_logs_equipment_id_fkey");
        entity.HasIndex(e => e.InspectionTime).IsDescending().HasDatabaseName("ix_water_quality_logs_inspection_time");
        entity.HasIndex(e => new { e.EquipmentId, e.SourceEventId }).IsUnique().HasFilter("source = 'Device'").HasDatabaseName("ux_water_quality_logs_device_event");
    }
}
