using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class CoachApplicationConfiguration : IEntityTypeConfiguration<CoachApplication>
{
    public void Configure(EntityTypeBuilder<CoachApplication> entity)
    {
        entity.ToTable("coach_applications", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_coach_applications_status",
                "status IN ('Pending', 'Approved', 'Rejected', 'Revoked')");
            table.HasCheckConstraint("ck_coach_applications_review_fields",
                "(status = 'Pending' AND reviewed_by_user_id IS NULL AND reviewed_at IS NULL) OR " +
                "(status <> 'Pending' AND reviewed_by_user_id IS NOT NULL AND reviewed_at IS NOT NULL)");
            table.HasCheckConstraint("ck_coach_applications_revoked_fields",
                "(status = 'Revoked' AND revoked_by_user_id IS NOT NULL AND revoked_at IS NOT NULL " +
                "AND revoke_reason IS NOT NULL AND length(btrim(revoke_reason)) > 0) OR " +
                "(status <> 'Revoked' AND revoked_by_user_id IS NULL AND revoked_at IS NULL " +
                "AND revoke_reason IS NULL)");
        });
        entity.HasKey(item => item.Id).HasName("coach_applications_pkey");
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.UserId).HasColumnName("user_id");
        entity.Property(item => item.CertificatePublicId).HasColumnName("certificate_public_id")
            .HasMaxLength(255).IsRequired();
        entity.Property(item => item.CertificateFormat).HasColumnName("certificate_format")
            .HasMaxLength(12).IsRequired();
        entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        entity.Property(item => item.SubmittedAt).HasColumnName("submitted_at")
            .HasColumnType("timestamp with time zone").IsRequired();
        entity.Property(item => item.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        entity.Property(item => item.ReviewedAt).HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");
        entity.Property(item => item.ReviewNote).HasColumnName("review_note").HasMaxLength(1000);
        entity.Property(item => item.RevokedByUserId).HasColumnName("revoked_by_user_id");
        entity.Property(item => item.RevokedAt).HasColumnName("revoked_at")
            .HasColumnType("timestamp with time zone");
        entity.Property(item => item.RevokeReason).HasColumnName("revoke_reason").HasMaxLength(1000);
        entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("coach_applications_user_id_fkey");
        entity.HasOne(item => item.ReviewedByUser).WithMany()
            .HasForeignKey(item => item.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("coach_applications_reviewed_by_user_id_fkey");
        entity.HasOne(item => item.RevokedByUser).WithMany()
            .HasForeignKey(item => item.RevokedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("coach_applications_revoked_by_user_id_fkey");
        entity.HasIndex(item => item.UserId).IsUnique()
            .HasFilter("status = 'Pending'").HasDatabaseName("ux_coach_applications_user_pending");
        entity.HasIndex(item => new { item.Status, item.SubmittedAt })
            .HasDatabaseName("ix_coach_applications_status_submitted_at");
    }
}
