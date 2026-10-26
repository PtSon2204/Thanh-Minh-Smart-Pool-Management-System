using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class ClassAttendanceConfiguration : IEntityTypeConfiguration<ClassAttendance>
{
    public void Configure(EntityTypeBuilder<ClassAttendance> entity)
    {
        entity.ToTable("class_attendances", "smart_pool", table =>
        {
            table.HasCheckConstraint(
                "ck_class_attendances_review_fields_pair",
                "(reviewed_by_user_id IS NULL) = (reviewed_at IS NULL)");
        });

        entity.HasKey(e => e.Id).HasName("class_attendances_pkey");
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
        entity.Property(e => e.RegistrationId).HasColumnName("registration_id").IsRequired();
        entity.Property(e => e.ImageUrl).HasColumnName("image_url").HasMaxLength(2048).IsRequired();
        entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        entity.Property(e => e.SubmittedByUserId).HasColumnName("submitted_by_user_id").IsRequired();
        entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone");
        entity.Property(e => e.ReviewNote).HasColumnName("review_note").HasMaxLength(500);
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

        entity.HasOne(e => e.Session)
            .WithMany(e => e.Attendances)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_attendances_session_id_fkey");
        entity.HasOne(e => e.Registration)
            .WithMany(e => e.Attendances)
            .HasForeignKey(e => e.RegistrationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_attendances_registration_id_fkey");
        entity.HasOne(e => e.SubmittedByUser)
            .WithMany()
            .HasForeignKey(e => e.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_attendances_submitted_by_user_id_fkey");
        entity.HasOne(e => e.ReviewedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("class_attendances_reviewed_by_user_id_fkey");

        entity.HasIndex(e => new { e.SessionId, e.RegistrationId })
            .IsUnique()
            .HasDatabaseName("ux_class_attendances_session_registration");
        entity.HasIndex(e => e.RegistrationId).HasDatabaseName("ix_class_attendances_registration_id");
        entity.HasIndex(e => new { e.Status, e.SubmittedAt }).HasDatabaseName("ix_class_attendances_status_submitted_at");
        entity.HasIndex(e => e.SubmittedByUserId).HasDatabaseName("ix_class_attendances_submitted_by_user_id");
        entity.HasIndex(e => e.ReviewedByUserId).HasDatabaseName("ix_class_attendances_reviewed_by_user_id");
    }
}
