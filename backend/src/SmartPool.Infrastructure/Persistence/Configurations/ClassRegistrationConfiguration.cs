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
            table.HasCheckConstraint(
                "ck_class_registrations_review_fields_pair",
                "(reviewed_by_user_id IS NULL) = (reviewed_at IS NULL)");
        });

        entity.HasKey(e => e.Id).HasName("class_registrations_pkey");
        entity.HasAlternateKey(e => new { e.Id, e.SessionId }).HasName("ak_class_registrations_id_session_id");
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
        entity.Property(e => e.CustomerUserId).HasColumnName("customer_user_id").IsRequired();
        entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        entity.Property(e => e.RequestedAt).HasColumnName("requested_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

        entity.HasOne(e => e.Session)
            .WithMany(e => e.Registrations)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_registrations_session_id_fkey");
        entity.HasOne(e => e.CustomerUser)
            .WithMany()
            .HasForeignKey(e => e.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_registrations_customer_user_id_fkey");
        entity.HasOne(e => e.ReviewedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("class_registrations_reviewed_by_user_id_fkey");

        entity.HasIndex(e => new { e.SessionId, e.CustomerUserId })
            .IsUnique()
            .HasDatabaseName("ux_class_registrations_session_customer");
        entity.HasIndex(e => new { e.CustomerUserId, e.Status })
            .HasDatabaseName("ix_class_registrations_customer_status");
        entity.HasIndex(e => new { e.Status, e.RequestedAt })
            .HasDatabaseName("ix_class_registrations_status_requested_at");
        entity.HasIndex(e => e.ReviewedByUserId).HasDatabaseName("ix_class_registrations_reviewed_by_user_id");
    }
}
