using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class MaintenanceLogConfiguration : IEntityTypeConfiguration<MaintenanceLog>
{
    public void Configure(EntityTypeBuilder<MaintenanceLog> entity)
    {
        entity.HasKey(e => e.Id).HasName("maintenance_logs_pkey");
        entity.ToTable("maintenance_logs", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.ActualDate).HasColumnName("actual_date");
        entity.Property(e => e.Cost).HasPrecision(18, 2).HasDefaultValueSql("0").HasColumnName("cost");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
        entity.Property(e => e.Note).HasColumnName("note");
        entity.Property(e => e.PerformedBy).HasColumnName("performed_by");
        entity.Property(e => e.ScheduleId).HasColumnName("schedule_id");
        entity.Property(e => e.VendorName).HasMaxLength(255).HasColumnName("vendor_name");
        entity.HasOne(d => d.Equipment).WithMany(p => p.MaintenanceLogs).HasForeignKey(d => d.EquipmentId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("maintenance_logs_equipment_id_fkey");
        entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.MaintenanceLogs).HasForeignKey(d => d.PerformedBy).HasConstraintName("maintenance_logs_performed_by_fkey");
        entity.HasOne(d => d.Schedule).WithMany(p => p.MaintenanceLogs).HasForeignKey(d => d.ScheduleId).HasConstraintName("maintenance_logs_schedule_id_fkey");
    }
}
