using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class StaffScheduleChangeConfiguration : IEntityTypeConfiguration<StaffScheduleDelivery>,
    IEntityTypeConfiguration<StaffScheduleChange>
{
    public void Configure(EntityTypeBuilder<StaffScheduleChange> builder)
    {
        builder.ToTable("staff_schedule_changes", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_staff_schedule_change_kind", "\"Kind\" IN ('Pattern','Leave','Move','Replace')");
            table.HasCheckConstraint("ck_staff_schedule_change_applied_version", "\"AppliedVersion\" IS NULL OR \"AppliedVersion\" > 0");
            table.HasCheckConstraint("ck_staff_schedule_change_outcome", "\"AppliedVersion\" IS NOT NULL OR \"OperationId\" IS NOT NULL");
            table.HasCheckConstraint("ck_staff_schedule_change_receipt", """
                ("OperationId" IS NULL AND "ProposalHash" IS NULL AND "ResponseJson" IS NULL)
                OR ("OperationId" IS NOT NULL AND "ProposalHash" IS NOT NULL AND "ResponseJson" IS NOT NULL
                    AND "ProposalHash" ~ '^[0-9A-Fa-f]{64}$' AND jsonb_typeof("ResponseJson") = 'object')
                """);
            table.HasCheckConstraint("ck_staff_schedule_change_instruction", """
                ("AppliedVersion" IS NULL AND "OperationId" IS NOT NULL)
                OR ("Kind" = 'Pattern' AND "ContractJson" IS NOT NULL AND jsonb_typeof("ContractJson") = 'object'
                    AND "EffectiveFrom" IS NOT NULL AND "WorkDate" IS NULL AND "SourceAssignmentIdsJson" IS NULL
                    AND "DestinationShiftId" IS NULL AND "DestinationEmployeeId" IS NULL AND "Reason" IS NULL)
                OR ("Kind" IN ('Leave','Move','Replace') AND "ContractJson" IS NULL AND "EffectiveFrom" IS NULL
                    AND "WorkDate" IS NOT NULL AND "SourceAssignmentIdsJson" IS NOT NULL
                    AND jsonb_typeof("SourceAssignmentIdsJson") = 'array' AND "Reason" IS NOT NULL AND btrim("Reason") <> '')
                """);
            table.HasCheckConstraint("ck_staff_schedule_change_destination", """
                "AppliedVersion" IS NULL OR "Kind" = 'Pattern'
                OR ("Kind" = 'Leave' AND "DestinationShiftId" IS NULL AND "DestinationEmployeeId" IS NULL)
                OR ("Kind" = 'Move' AND "DestinationShiftId" IS NOT NULL AND "DestinationEmployeeId" IS NULL
                    AND jsonb_array_length("SourceAssignmentIdsJson") = 1)
                OR ("Kind" = 'Replace' AND "DestinationShiftId" IS NOT NULL AND "DestinationEmployeeId" IS NOT NULL
                    AND "DestinationEmployeeId" <> "EmployeeId" AND jsonb_array_length("SourceAssignmentIdsJson") = 1)
                """);
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasMaxLength(16);
        builder.Property(item => item.ContractJson).HasColumnType("jsonb");
        builder.Property(item => item.SourceAssignmentIdsJson).HasColumnType("jsonb");
        builder.Property(item => item.ResponseJson).HasColumnType("jsonb");
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.ProposalHash).HasMaxLength(64);
        builder.HasIndex(item => new { item.EmployeeId, item.AppliedVersion }).IsUnique()
            .HasFilter("\"AppliedVersion\" IS NOT NULL");
        builder.HasIndex(item => item.OperationId).IsUnique().HasFilter("\"OperationId\" IS NOT NULL");
        builder.HasIndex(item => new { item.EmployeeId, item.WorkDate })
            .HasFilter("\"AppliedVersion\" IS NOT NULL AND \"WorkDate\" IS NOT NULL");
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.DestinationEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Shift>().WithMany().HasForeignKey(item => item.DestinationShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
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
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.RecipientId).OnDelete(DeleteBehavior.Restrict);
    }
}
