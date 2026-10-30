using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class RentalPaymentIntentConfiguration : IEntityTypeConfiguration<RentalPaymentIntent>
{
    public void Configure(EntityTypeBuilder<RentalPaymentIntent> entity)
    {
        entity.ToTable("rental_payment_intents", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_rental_payment_intents_status", "status IN ('PENDING', 'PARTIAL', 'PAID', 'ISSUED', 'EXPIRED', 'CANCELLED')");
            table.HasCheckConstraint("ck_rental_payment_intents_nonnegative", "fee_amount >= 0 AND deposit_amount >= 0");
        });
        entity.HasKey(item => item.Id).HasName("rental_payment_intents_pkey");
        entity.Property(item => item.Id).ValueGeneratedNever().HasColumnName("id");
        entity.Property(item => item.OrderId).HasColumnName("order_id");
        entity.Property(item => item.InitiatedByUserId).HasColumnName("initiated_by_user_id");
        entity.Property(item => item.RequestHash).HasMaxLength(64).IsRequired().HasColumnName("request_hash");
        entity.Property(item => item.Reference).HasMaxLength(64).IsRequired().HasColumnName("reference");
        entity.Property(item => item.Status).HasMaxLength(20).IsRequired().HasColumnName("status");
        entity.Property(item => item.LinesJson).IsRequired().HasColumnType("text").HasColumnName("lines_json");
        entity.Property(item => item.FeeAmount).HasPrecision(18, 2).HasColumnName("fee_amount");
        entity.Property(item => item.DepositAmount).HasPrecision(18, 2).HasColumnName("deposit_amount");
        entity.Property(item => item.CreatedAtUtc).HasColumnType("timestamp with time zone").HasColumnName("created_at_utc");
        entity.Property(item => item.ExpiresAtUtc).HasColumnType("timestamp with time zone").HasColumnName("expires_at_utc");
        entity.Property(item => item.PaidAtUtc).HasColumnType("timestamp with time zone").HasColumnName("paid_at_utc");
        entity.Property(item => item.IssuedAtUtc).HasColumnType("timestamp with time zone").HasColumnName("issued_at_utc");
        entity.Property(item => item.IssuedByUserId).HasColumnName("issued_by_user_id");
        entity.HasIndex(item => item.OrderId).IsUnique().HasDatabaseName("ux_rental_payment_intents_order");
        entity.HasIndex(item => item.Reference).IsUnique().HasDatabaseName("ux_rental_payment_intents_reference");
        entity.HasIndex(item => new { item.Status, item.ExpiresAtUtc }).HasDatabaseName("ix_rental_payment_intents_status_expiry");
        entity.HasOne<Order>().WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<User>().WithMany().HasForeignKey(item => item.InitiatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<User>().WithMany().HasForeignKey(item => item.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
