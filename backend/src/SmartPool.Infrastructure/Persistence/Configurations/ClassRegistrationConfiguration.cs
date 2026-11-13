using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class ClassRegistrationConfiguration : IEntityTypeConfiguration<ClassRegistration>
{
    public void Configure(EntityTypeBuilder<ClassRegistration> entity)
    {
        entity.ToTable("class_registrations", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_class_registrations_confirmed_after_registered", "confirmed_at IS NULL OR confirmed_at >= registered_at");
            table.HasCheckConstraint("ck_class_registrations_cancelled_after_registered", "cancelled_at IS NULL OR cancelled_at >= registered_at");
            table.HasCheckConstraint("ck_class_registrations_status", "status IN ('PendingApproval', 'Confirmed', 'Rejected', 'Cancelled')");
            table.HasCheckConstraint("ck_class_registrations_review_fields_pair", "(reviewed_by_user_id IS NULL) = (reviewed_at IS NULL)");
            table.HasCheckConstraint("ck_class_registrations_reviewed_after_registered", "reviewed_at IS NULL OR reviewed_at >= registered_at");
            table.HasCheckConstraint("ck_class_registrations_pending_fields", "status <> 'PendingApproval' OR (confirmed_at IS NULL AND reviewed_by_user_id IS NULL AND reviewed_at IS NULL AND review_note IS NULL)");
            table.HasCheckConstraint("ck_class_registrations_rejected_fields", "status <> 'Rejected' OR (confirmed_at IS NULL AND reviewed_by_user_id IS NOT NULL AND reviewed_at IS NOT NULL AND review_note IS NOT NULL AND btrim(review_note) <> '')");
        });

        entity.HasKey(e => e.Id).HasName("class_registrations_pkey");
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.ClassId).HasColumnName("class_id").IsRequired();
        entity.Property(e => e.CustomerUserId).HasColumnName("customer_user_id").IsRequired();
        entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        entity.Property(e => e.RegisteredAt).HasColumnName("registered_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamp with time zone");
        entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamp with time zone");
        entity.Property(e => e.CancelledByUserId).HasColumnName("cancelled_by_user_id");
        entity.Property(e => e.CancelReason).HasColumnName("cancel_reason").HasMaxLength(500);
        entity.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone");
        entity.Property(e => e.ReviewNote).HasColumnName("review_note").HasMaxLength(500);
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

        entity.HasOne(e => e.Class)
            .WithMany(e => e.Registrations)
            .HasForeignKey(e => e.ClassId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_registrations_class_id_fkey");
        entity.HasOne(e => e.CustomerUser)
            .WithMany()
            .HasForeignKey(e => e.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_registrations_customer_user_id_fkey");
        entity.HasOne<User>().WithMany().HasForeignKey(e => e.CancelledByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("class_registrations_cancelled_by_user_id_fkey");
        entity.HasOne<User>().WithMany().HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_registrations_reviewed_by_user_id_fkey");

        entity.HasIndex(e => new { e.ClassId, e.CustomerUserId })
            .IsUnique()
            .HasDatabaseName("ux_class_registrations_class_customer");
        entity.HasIndex(e => new { e.CustomerUserId, e.Status })
            .HasDatabaseName("ix_class_registrations_customer_status");
        entity.HasIndex(e => new { e.Status, e.RegisteredAt })
            .HasDatabaseName("ix_class_registrations_status_registered_at");
        entity.HasIndex(e => e.ReviewedByUserId)
            .HasDatabaseName("ix_class_registrations_reviewed_by_user_id");
    }
}
