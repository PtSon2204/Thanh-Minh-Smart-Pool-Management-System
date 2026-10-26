using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class SwimClassConfiguration : IEntityTypeConfiguration<SwimClass>
{
    public void Configure(EntityTypeBuilder<SwimClass> entity)
    {
        entity.ToTable("swim_classes", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_swim_classes_capacity_positive", "capacity > 0");
            table.HasCheckConstraint("ck_swim_classes_fee_nonnegative", "fee >= 0");
            table.HasCheckConstraint("ck_swim_classes_registration_time_order", "registration_start_at < registration_end_at");
        });

        entity.HasKey(e => e.Id).HasName("swim_classes_pkey");
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        entity.Property(e => e.Description).HasColumnName("description");
        entity.Property(e => e.Capacity).HasColumnName("capacity").IsRequired();
        entity.Property(e => e.Fee).HasColumnName("fee").HasPrecision(18, 2).IsRequired();
        entity.Property(e => e.RegistrationStartAt).HasColumnName("registration_start_at").HasColumnType("timestamp with time zone").IsRequired();
        entity.Property(e => e.RegistrationEndAt).HasColumnName("registration_end_at").HasColumnType("timestamp with time zone").IsRequired();
        entity.Property(e => e.CoachUserId).HasColumnName("coach_user_id").IsRequired();
        entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

        entity.HasOne(e => e.CoachUser)
            .WithMany()
            .HasForeignKey(e => e.CoachUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("swim_classes_coach_user_id_fkey");
        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("swim_classes_created_by_user_id_fkey");

        entity.HasIndex(e => e.CoachUserId).HasDatabaseName("ix_swim_classes_coach_user_id");
        entity.HasIndex(e => e.Status).HasDatabaseName("ix_swim_classes_status");
    }
}
