using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> entity)
    {
        entity.HasKey(e => e.Id).HasName("maintenance_schedules_pkey");
        entity.ToTable("maintenance_schedules", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.Description).HasColumnName("description");
        entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
        entity.Property(e => e.PlannedDate).HasColumnName("planned_date");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Pending'::character varying").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.MaintenanceSchedules).HasForeignKey(d => d.AssignedTo).HasConstraintName("maintenance_schedules_assigned_to_fkey");
        entity.HasOne(d => d.Equipment).WithMany(p => p.MaintenanceSchedules).HasForeignKey(d => d.EquipmentId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("maintenance_schedules_equipment_id_fkey");
    }
}
