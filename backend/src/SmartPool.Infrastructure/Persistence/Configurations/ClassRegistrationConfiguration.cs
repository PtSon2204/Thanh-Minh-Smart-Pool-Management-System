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
            table.HasCheckConstraint("ck_class_registrations_status", "status IN ('Confirmed', 'Cancelled')");
        });

        entity.HasKey(e => e.Id).HasName("class_registrations_pkey");
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.ClassId).HasColumnName("class_id").IsRequired();
        entity.Property(e => e.CustomerUserId).HasColumnName("customer_user_id").IsRequired();
        entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        entity.Property(e => e.RegisteredAt).HasColumnName("registered_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamp with time zone");
        entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamp with time zone");
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

        entity.HasIndex(e => new { e.ClassId, e.CustomerUserId })
            .IsUnique()
            .HasDatabaseName("ux_class_registrations_class_customer");
        entity.HasIndex(e => new { e.CustomerUserId, e.Status })
            .HasDatabaseName("ix_class_registrations_customer_status");
        entity.HasIndex(e => new { e.Status, e.RegisteredAt })
            .HasDatabaseName("ix_class_registrations_status_registered_at");
    }
}
