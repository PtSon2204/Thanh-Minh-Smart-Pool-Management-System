using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class PoolDutyScheduleConfiguration : IEntityTypeConfiguration<PoolDutySchedule>
{
    public void Configure(EntityTypeBuilder<PoolDutySchedule> entity)
    {
        entity.ToTable("pool_duty_schedules", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_pool_duty_schedules_status",
                "status IN ('Scheduled', 'InProgress', 'Completed', 'Absent', 'Cancelled', 'Unassigned')");
            table.HasCheckConstraint("ck_pool_duty_schedules_assignment_type",
                "assignment_type IN ('Auto', 'Manual')");
        });

        entity.HasKey(e => e.Id).HasName("pool_duty_schedules_pkey");

        entity.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("uuid_generate_v4()");

        entity.Property(e => e.DutyDate)
            .HasColumnName("duty_date")
            .IsRequired();

        entity.Property(e => e.ShiftId)
            .HasColumnName("shift_id")
            .IsRequired();

        entity.Property(e => e.AssignedEmployeeId)
            .HasColumnName("assigned_employee_id")
            .IsRequired(false);

        entity.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("Scheduled");

        entity.Property(e => e.AssignmentType)
            .HasColumnName("assignment_type")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("Auto");

        entity.Property(e => e.WaterQualityLogId)
            .HasColumnName("water_quality_log_id");

        entity.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        entity.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        entity.HasOne(e => e.Shift)
            .WithMany()
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_shift_id_fkey");

        entity.HasOne(e => e.AssignedEmployee)
            .WithMany()
            .HasForeignKey(e => e.AssignedEmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_assigned_employee_id_fkey");

        entity.HasOne(e => e.WaterQualityLog)
            .WithMany()
            .HasForeignKey(e => e.WaterQualityLogId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("pool_duty_schedules_water_quality_log_id_fkey");

        entity.HasIndex(e => new { e.DutyDate, e.ShiftId })
            .HasDatabaseName("ix_pool_duty_schedules_date_shift");
    }
}

