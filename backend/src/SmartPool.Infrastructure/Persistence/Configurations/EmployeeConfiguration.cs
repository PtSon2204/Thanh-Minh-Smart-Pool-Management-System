using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.HasKey(e => e.UserId).HasName("employees_pkey");
        entity.ToTable("employees", "smart_pool", table => table.HasCheckConstraint("ck_employees_correction_active", "(correction_request_id IS NULL AND correction_reason IS NULL AND correction_requested_at_utc IS NULL AND correction_revision_id IS NULL) OR (correction_request_id IS NOT NULL AND correction_reason IS NOT NULL AND btrim(correction_reason) <> '' AND correction_requested_at_utc IS NOT NULL)"));
        entity.Property(e => e.UserId).ValueGeneratedNever().HasColumnName("user_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.JoinDate).HasColumnName("join_date");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Working'::character varying").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.Property(e => e.CorrectionRequestId).HasColumnName("correction_request_id");
        entity.Property(e => e.CorrectionReason).HasMaxLength(1000).HasColumnName("correction_reason");
        entity.Property(e => e.CorrectionRequestedAtUtc).HasColumnName("correction_requested_at_utc");
        entity.Property(e => e.CorrectionRevisionId).HasColumnName("correction_revision_id");
        entity.Property(e => e.CorrectionVersion).HasDefaultValue(0L).IsConcurrencyToken().HasColumnName("correction_version");
        entity.HasOne<StaffOnboardingRevision>().WithMany().HasForeignKey(e => e.CorrectionRevisionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(d => d.User).WithOne(p => p.Employee).HasForeignKey<Employee>(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("employees_user_id_fkey");
    }
}
