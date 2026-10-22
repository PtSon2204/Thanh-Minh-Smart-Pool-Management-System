using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class ClassSessionConfiguration : IEntityTypeConfiguration<ClassSession>
{
    public void Configure(EntityTypeBuilder<ClassSession> entity)
    {
        entity.ToTable("class_sessions", "smart_pool", table =>
            table.HasCheckConstraint("ck_class_sessions_time_order", "starts_at < ends_at"));

        entity.HasKey(e => e.Id).HasName("class_sessions_pkey");
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.ClassId).HasColumnName("class_id").IsRequired();
        entity.Property(e => e.CoachUserId).HasColumnName("coach_user_id").IsRequired();
        entity.Property(e => e.StartsAt).HasColumnName("starts_at").HasColumnType("timestamp with time zone").IsRequired();
        entity.Property(e => e.EndsAt).HasColumnName("ends_at").HasColumnType("timestamp with time zone").IsRequired();
        entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        entity.Property(e => e.CancelReason).HasColumnName("cancel_reason").HasMaxLength(500);
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

        entity.HasOne(e => e.Class)
            .WithMany(e => e.Sessions)
            .HasForeignKey(e => e.ClassId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_sessions_class_id_fkey");
        entity.HasOne(e => e.CoachUser)
            .WithMany()
            .HasForeignKey(e => e.CoachUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("class_sessions_coach_user_id_fkey");

        entity.HasIndex(e => new { e.ClassId, e.StartsAt }).HasDatabaseName("ix_class_sessions_class_starts_at");
        entity.HasIndex(e => new { e.CoachUserId, e.StartsAt }).HasDatabaseName("ix_class_sessions_coach_starts_at");
    }
}
