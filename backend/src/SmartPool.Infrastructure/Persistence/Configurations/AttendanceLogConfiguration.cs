using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class AttendanceLogConfiguration : IEntityTypeConfiguration<AttendanceLog>
{
    public void Configure(EntityTypeBuilder<AttendanceLog> entity)
    {
        entity.HasKey(e => e.Id).HasName("attendance_logs_pkey");
        entity.ToTable("attendance_logs", "smart_pool");
        entity.HasIndex(e => e.EmployeeShiftId, "ix_attendance_logs_employee_shift_id");
        entity.HasIndex(e => e.PerformedAtUtc, "ix_attendance_logs_performed_at_utc");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.Action).HasMaxLength(20).HasColumnName("action");
        entity.Property(e => e.Method).HasMaxLength(20).HasColumnName("method");
        entity.Property(e => e.PerformedByUserId).HasColumnName("performed_by_user_id");
        entity.Property(e => e.PerformedAtUtc).HasColumnName("performed_at_utc");
        entity.Property(e => e.Reason).HasColumnName("reason");
        entity.Property(e => e.EmployeeShiftId).HasColumnName("employee_shift_id");
        entity.Property(e => e.BeforeCheckInTime).HasColumnName("before_check_in_time");
        entity.Property(e => e.BeforeCheckOutTime).HasColumnName("before_check_out_time");
        entity.Property(e => e.BeforeCheckInMethod).HasMaxLength(20).HasColumnName("before_check_in_method");
        entity.Property(e => e.BeforeCheckOutMethod).HasMaxLength(20).HasColumnName("before_check_out_method");
        entity.Property(e => e.BeforeStatus).HasMaxLength(50).HasColumnName("before_status");
        entity.Property(e => e.AfterCheckInTime).HasColumnName("after_check_in_time");
        entity.Property(e => e.AfterCheckOutTime).HasColumnName("after_check_out_time");
        entity.Property(e => e.AfterCheckInMethod).HasMaxLength(20).HasColumnName("after_check_in_method");
        entity.Property(e => e.AfterCheckOutMethod).HasMaxLength(20).HasColumnName("after_check_out_method");
        entity.Property(e => e.AfterStatus).HasMaxLength(50).HasColumnName("after_status");
        entity.HasOne(d => d.EmployeeShift).WithMany(p => p.AttendanceLogs).HasForeignKey(d => d.EmployeeShiftId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("attendance_logs_employee_shift_id_fkey");
        entity.HasOne(d => d.PerformedByUser).WithMany(p => p.PerformedAttendanceLogs).HasForeignKey(d => d.PerformedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("attendance_logs_performed_by_user_id_fkey");
    }
}
