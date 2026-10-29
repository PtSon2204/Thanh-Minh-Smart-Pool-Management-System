using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class DailyInspectionConfiguration : IEntityTypeConfiguration<DailyInspection>
{
    public void Configure(EntityTypeBuilder<DailyInspection> entity)
    {
        entity.HasKey(e => e.Id).HasName("daily_inspections_pkey");
        entity.ToTable("daily_inspections", "smart_pool");

        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.InspectorId).HasColumnName("inspector_id");
        entity.Property(e => e.ShiftId).HasColumnName("shift_id");
        entity.Property(e => e.InspectionDate).HasColumnName("inspection_date");
        entity.Property(e => e.InspectionTime).HasColumnName("inspection_time");
        entity.Property(e => e.ItemsJson).HasColumnType("jsonb").HasColumnName("items_json");
        entity.Property(e => e.OverallStatus).HasMaxLength(50).HasDefaultValueSql("'Passed'::character varying").HasColumnName("overall_status");
        entity.Property(e => e.Notes).HasMaxLength(1000).HasColumnName("notes");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");

        entity.HasOne(d => d.Inspector)
            .WithMany(p => p.DailyInspections)
            .HasForeignKey(d => d.InspectorId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("daily_inspections_inspector_id_fkey");

        entity.HasOne(d => d.Shift)
            .WithMany()
            .HasForeignKey(d => d.ShiftId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("daily_inspections_shift_id_fkey");
    }
}
