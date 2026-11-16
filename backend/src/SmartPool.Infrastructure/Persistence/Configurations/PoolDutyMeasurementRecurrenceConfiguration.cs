using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class PoolDutyMeasurementRecurrenceConfiguration : IEntityTypeConfiguration<PoolDutyMeasurementRecurrence>
{
    public void Configure(EntityTypeBuilder<PoolDutyMeasurementRecurrence> entity)
    {
        entity.ToTable("pool_duty_measurement_recurrences", "smart_pool");
        entity.HasKey(item => item.Id).HasName("pool_duty_measurement_recurrences_pkey");
        entity.Property(item => item.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(item => item.AnchorDate).HasColumnName("anchor_date");
        entity.Property(item => item.NextOccurrenceDate).HasColumnName("next_occurrence_date");
        entity.Property(item => item.ScheduledTime).HasColumnName("scheduled_time");
        entity.Property(item => item.ShiftId).HasColumnName("shift_id");
        entity.Property(item => item.PreferredEmployeeId).HasColumnName("preferred_employee_id");
        entity.Property(item => item.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        entity.Property(item => item.CreatedBy).HasColumnName("created_by");
        entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        entity.HasOne<Shift>().WithMany().HasForeignKey(item => item.ShiftId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("pool_duty_measurement_recurrences_shift_id_fkey");
        entity.HasOne<Employee>().WithMany().HasForeignKey(item => item.PreferredEmployeeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("pool_duty_measurement_recurrences_employee_id_fkey");
        entity.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("pool_duty_measurement_recurrences_created_by_fkey");
        entity.HasIndex(item => new { item.IsActive, item.NextOccurrenceDate })
            .HasDatabaseName("ix_pool_duty_measurement_recurrences_active_next_occurrence");
        entity.HasIndex(item => new { item.AnchorDate, item.ScheduledTime, item.ShiftId, item.PreferredEmployeeId })
            .IsUnique().HasFilter("is_active = true")
            .HasDatabaseName("ux_pool_duty_measurement_recurrences_active_pattern");
    }
}
