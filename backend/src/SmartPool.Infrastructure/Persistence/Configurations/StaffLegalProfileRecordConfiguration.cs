using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class StaffLegalProfileRecordConfiguration : IEntityTypeConfiguration<StaffLegalProfileRecord>
{
    public void Configure(EntityTypeBuilder<StaffLegalProfileRecord> builder)
    {
        builder.ToTable("staff_legal_profiles");
        builder.HasKey(item => item.EmployeeId).HasName("staff_legal_profiles_pkey");
        builder.Property(item => item.EmployeeId).ValueGeneratedNever().HasColumnName("employee_id");
        builder.Property(item => item.ProtectedProfile).HasColumnName("protected_profile");
        builder.Property(item => item.CccdDigest).HasMaxLength(64).HasColumnName("cccd_digest");
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(item => item.Version).IsConcurrencyToken().HasDefaultValue(1L).HasColumnName("version");
        builder.HasIndex(item => item.CccdDigest, "staff_legal_profiles_cccd_digest_key").IsUnique();
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("staff_legal_profiles_employee_id_fkey");
    }
}
