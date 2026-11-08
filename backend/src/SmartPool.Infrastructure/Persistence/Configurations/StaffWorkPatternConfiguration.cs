using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class StaffWorkPatternConfiguration : IEntityTypeConfiguration<StaffWorkPattern>,
    IEntityTypeConfiguration<StaffScheduleException>, IEntityTypeConfiguration<StaffSchedulePreview>,
    IEntityTypeConfiguration<StaffScheduleOperation>, IEntityTypeConfiguration<StaffScheduleDelivery>
{
    public void Configure(EntityTypeBuilder<StaffWorkPattern> builder)
    {
        builder.ToTable("staff_work_patterns");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.EmployeeId, item.Version }).IsUnique();
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<StaffScheduleException> builder)
    {
        builder.ToTable("staff_schedule_exceptions", table => table.HasCheckConstraint("ck_schedule_exception_kind", "\"Kind\" IN ('Leave','Move','Replace')"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.EmployeeId, item.WorkDate });
        builder.HasIndex(item => new { item.EmployeeId, item.Version }).IsUnique();
        builder.HasIndex(item => item.DestinationEmployeeId);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.DestinationEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Shift>().WithMany().HasForeignKey(item => item.DestinationShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<StaffSchedulePreview> builder)
    {
        builder.ToTable("staff_schedule_previews");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ProposalHash).HasMaxLength(64);
        builder.Property(item => item.StateHash).HasMaxLength(64);
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<StaffScheduleOperation> builder)
    {
        builder.ToTable("staff_schedule_operations");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.EmployeeId, item.CreatedAtUtc });
        builder.Property(item => item.ProposalHash).HasMaxLength(64);
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<StaffScheduleDelivery> builder)
    {
        builder.ToTable("staff_schedule_deliveries", "smart_pool", table =>
            table.HasCheckConstraint("ck_staff_schedule_delivery_status", "\"Status\" IN ('Pending','Sending','Sent','Failed','Unknown')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Channel).HasMaxLength(16);
        builder.Property(item => item.RecipientEmail).HasMaxLength(320);
        builder.Property(item => item.Subject).HasMaxLength(255);
        builder.Property(item => item.LastErrorCode).HasMaxLength(64);
        builder.HasIndex(item => new { item.OperationId, item.RecipientId, item.Channel }).IsUnique();
        builder.HasIndex(item => new { item.Status, item.NextAttemptAtUtc, item.CreatedAtUtc });
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.RecipientId).OnDelete(DeleteBehavior.Restrict);
    }
}
