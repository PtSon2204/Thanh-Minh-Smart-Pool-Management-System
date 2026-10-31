using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class RentalTransactionConfiguration : IEntityTypeConfiguration<RentalTransaction>
{
    public void Configure(EntityTypeBuilder<RentalTransaction> entity)
    {
        entity.ToTable("rental_transactions", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_rental_transactions_nonnegative", "fee_amount >= 0 AND deposit_amount >= 0 AND refund_amount >= 0 AND retained_amount >= 0");
            table.HasCheckConstraint("ck_rental_transactions_kind", "kind IN ('Collection', 'Settlement')");
            table.HasCheckConstraint("ck_rental_transactions_amounts", "(kind = 'Collection' AND refund_amount = 0 AND retained_amount = 0 AND product_id IS NULL) OR (kind = 'Settlement' AND fee_amount = 0 AND deposit_amount = refund_amount + retained_amount AND product_id IS NOT NULL)");
            table.HasCheckConstraint("ck_rental_transactions_refund_method", "refund_method IS NULL OR refund_method = 'CASH'");
        });
        entity.HasKey(item => item.Id).HasName("rental_transactions_pkey");
        entity.Property(item => item.Id).ValueGeneratedNever().HasColumnName("id");
        entity.Property(item => item.OrderId).HasColumnName("order_id");
        entity.Property(item => item.ProductId).HasColumnName("product_id");
        entity.Property(item => item.PaymentId).HasColumnName("payment_id");
        entity.Property(item => item.Kind).HasMaxLength(20).IsRequired().HasColumnName("kind");
        entity.Property(item => item.RequestHash).HasMaxLength(64).IsRequired().HasColumnName("request_hash");
        entity.Property(item => item.FeeAmount).HasPrecision(18, 2).HasColumnName("fee_amount");
        entity.Property(item => item.DepositAmount).HasPrecision(18, 2).HasColumnName("deposit_amount");
        entity.Property(item => item.RefundAmount).HasPrecision(18, 2).HasColumnName("refund_amount");
        entity.Property(item => item.RetainedAmount).HasPrecision(18, 2).HasColumnName("retained_amount");
        entity.Property(item => item.RefundMethod).HasMaxLength(20).HasColumnName("refund_method");
        entity.Property(item => item.ConfirmedByUserId).HasColumnName("confirmed_by_user_id");
        entity.Property(item => item.ConfirmedAtUtc).HasColumnType("timestamp with time zone").HasColumnName("confirmed_at_utc");
        entity.Property(item => item.Reason).HasMaxLength(500).HasColumnName("reason");
        entity.Property(item => item.ResultJson).IsRequired().HasColumnType("text").HasColumnName("result_json");
        entity.HasIndex(item => item.OrderId).IsUnique().HasFilter("kind = 'Collection'").HasDatabaseName("ux_rental_transactions_collection_order");
        entity.HasIndex(item => new { item.OrderId, item.ConfirmedAtUtc }).HasDatabaseName("ix_rental_transactions_order_time");
        entity.HasOne<Order>().WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Payment>().WithMany().HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<User>().WithMany().HasForeignKey(item => item.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
