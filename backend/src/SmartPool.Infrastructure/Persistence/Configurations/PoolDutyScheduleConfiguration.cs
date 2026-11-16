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
            table.HasCheckConstraint("ck_pool_duty_schedules_task_type",
                "task_type IS NULL OR task_type IN ('Measurement', 'EquipmentChecklist', 'Cleaning')");
            table.HasCheckConstraint("ck_pool_duty_schedules_inspection_window",
                "inspection_window IS NULL OR inspection_window IN ('MORNING', 'AFTERNOON')");
            table.HasCheckConstraint("ck_pool_duty_schedules_task_links",
                "(task_type IS NULL) OR " +
                "(task_type = 'Measurement' AND inspection_window IS NULL AND trigger_water_quality_log_id IS NULL) OR " +
                "(task_type = 'EquipmentChecklist' AND inspection_window IS NOT NULL AND measurement_recurrence_id IS NULL AND trigger_water_quality_log_id IS NULL) OR " +
                "(task_type = 'Cleaning' AND trigger_water_quality_log_id IS NOT NULL AND measurement_recurrence_id IS NULL AND inspection_window IS NULL)");
            table.HasCheckConstraint("ck_pool_duty_schedules_assigned_shift",
                "task_type IS NULL OR assigned_employee_id IS NULL OR employee_shift_id IS NOT NULL");
            table.HasCheckConstraint("ck_pool_duty_schedules_unassigned_cleaning",
                "task_type <> 'Cleaning' OR assigned_employee_id IS NULL OR (employee_shift_id IS NOT NULL AND due_at_utc IS NOT NULL)");
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
            .IsRequired(false);

        entity.Property(e => e.EmployeeShiftId).HasColumnName("employee_shift_id");
        entity.Property(e => e.TaskType).HasColumnName("task_type").HasMaxLength(32);
        entity.Property(e => e.InspectionWindow).HasColumnName("inspection_window").HasMaxLength(16);
        entity.Property(e => e.ScheduledAtUtc).HasColumnName("scheduled_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(e => e.DueAtUtc).HasColumnName("due_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(e => e.DueNoticeSentAtUtc).HasColumnName("due_notice_sent_at_utc")
            .HasColumnType("timestamp with time zone").IsConcurrencyToken();
        entity.Property(e => e.MeasurementRecurrenceId).HasColumnName("measurement_recurrence_id");
        entity.Property(e => e.TriggerWaterQualityLogId).HasColumnName("trigger_water_quality_log_id");
        entity.Property(e => e.UnassignedReason).HasColumnName("unassigned_reason").HasMaxLength(64);
        entity.Property(e => e.Revision).HasColumnName("revision").IsConcurrencyToken().HasDefaultValue(1L);
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        entity.Property(e => e.AssignedEmployeeId)
            .HasColumnName("assigned_employee_id")
            .IsRequired(false);

        entity.Property(e => e.PreferredEmployeeId).HasColumnName("preferred_employee_id");

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

        entity.Property(e => e.LastExecutedBy).HasColumnName("last_executed_by");
        entity.Property(e => e.LastExecutedAtUtc).HasColumnName("last_executed_at_utc")
            .HasColumnType("timestamp with time zone");
        entity.Property(e => e.LastExecutionAssigneeId).HasColumnName("last_execution_assignee_id");
        entity.Property(e => e.LastExecutionWasAdminOverride).HasColumnName("last_execution_was_admin_override");
        entity.Property(e => e.DailyInspectionId).HasColumnName("daily_inspection_id");

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

        entity.HasOne<EmployeeShift>()
            .WithMany()
            .HasForeignKey(e => e.EmployeeShiftId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_employee_shift_id_fkey");

        entity.HasOne<PoolDutyMeasurementRecurrence>()
            .WithMany()
            .HasForeignKey(e => e.MeasurementRecurrenceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_measurement_recurrence_id_fkey");

        entity.HasOne<WaterQualityLog>()
            .WithMany()
            .HasForeignKey(e => e.TriggerWaterQualityLogId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_trigger_water_quality_log_id_fkey");

        entity.HasOne(e => e.AssignedEmployee)
            .WithMany()
            .HasForeignKey(e => e.AssignedEmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_assigned_employee_id_fkey");

        entity.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(e => e.PreferredEmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_preferred_employee_id_fkey");

        entity.HasOne(e => e.WaterQualityLog)
            .WithMany()
            .HasForeignKey(e => e.WaterQualityLogId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("pool_duty_schedules_water_quality_log_id_fkey");

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.LastExecutedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_last_executed_by_fkey");

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.LastExecutionAssigneeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_last_execution_assignee_id_fkey");

        entity.HasOne<DailyInspection>()
            .WithMany()
            .HasForeignKey(e => e.DailyInspectionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pool_duty_schedules_daily_inspection_id_fkey");

        entity.HasIndex(e => new { e.DutyDate, e.ShiftId })
            .HasDatabaseName("ix_pool_duty_schedules_date_shift");

        entity.HasIndex(e => new { e.MeasurementRecurrenceId, e.DutyDate })
            .IsUnique()
            .HasFilter("task_type = 'Measurement' AND measurement_recurrence_id IS NOT NULL")
            .HasDatabaseName("ux_pool_duty_schedules_measurement_occurrence");

        entity.HasIndex(e => new { e.DutyDate, e.InspectionWindow })
            .IsUnique()
            .HasFilter("task_type = 'EquipmentChecklist' AND inspection_window IS NOT NULL")
            .HasDatabaseName("ux_pool_duty_schedules_checklist_window");

        entity.HasIndex(e => e.TriggerWaterQualityLogId)
            .IsUnique()
            .HasFilter("task_type = 'Cleaning' AND trigger_water_quality_log_id IS NOT NULL")
            .HasDatabaseName("ux_pool_duty_schedules_cleaning_trigger");

        entity.HasIndex(e => new { e.AssignedEmployeeId, e.DutyDate })
            .HasDatabaseName("ix_pool_duty_schedules_employee_date");

        entity.HasIndex(e => new { e.Status, e.DutyDate })
            .HasDatabaseName("ix_pool_duty_schedules_status_date");
    }
}

