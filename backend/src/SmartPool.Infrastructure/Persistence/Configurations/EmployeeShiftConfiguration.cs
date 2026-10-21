using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class EmployeeShiftConfiguration : IEntityTypeConfiguration<EmployeeShift>
{
    public void Configure(EntityTypeBuilder<EmployeeShift> entity)
    {
        entity.HasKey(e => e.Id).HasName("employee_shifts_pkey");
        entity.ToTable("employee_shifts", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_employee_shifts_checkout_requires_checkin", "check_out_time IS NULL OR check_in_time IS NOT NULL");
            table.HasCheckConstraint("ck_employee_shifts_time_order", "check_out_time IS NULL OR check_out_time >= check_in_time");
        });
        entity.HasIndex(e => new { e.EmployeeId, e.ShiftId, e.WorkDate }, "ux_employee_shifts_employee_id_shift_id_work_date").IsUnique();
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.CheckInTime).HasColumnName("check_in_time");
        entity.Property(e => e.CheckInMethod).HasMaxLength(20).HasColumnName("check_in_method");
        entity.Property(e => e.CheckOutTime).HasColumnName("check_out_time");
        entity.Property(e => e.CheckOutMethod).HasMaxLength(20).HasColumnName("check_out_method");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
        entity.Property(e => e.ShiftId).HasColumnName("shift_id");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Scheduled'::character varying").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.Property(e => e.WorkDate).HasColumnName("work_date");
        entity.HasOne(d => d.Employee).WithMany(p => p.EmployeeShifts).HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("employee_shifts_employee_id_fkey");
        entity.HasOne(d => d.Shift).WithMany(p => p.EmployeeShifts).HasForeignKey(d => d.ShiftId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("employee_shifts_shift_id_fkey");
    }
}
