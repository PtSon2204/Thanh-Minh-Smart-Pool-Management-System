using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> entity)
    {
        entity.HasKey(e => e.Id).HasName("payments_pkey");
        entity.ToTable("payments", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.Amount).HasPrecision(18, 2).HasColumnName("amount");
        entity.Property(e => e.CashierId).HasColumnName("cashier_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.OrderId).HasColumnName("order_id");
        entity.Property(e => e.PaymentMethod).HasMaxLength(50).HasColumnName("payment_method");
        entity.Property(e => e.PaymentTime).HasDefaultValueSql("now()").HasColumnName("payment_time");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Completed'::character varying").HasColumnName("status");
        entity.Property(e => e.TransactionRef).HasMaxLength(255).HasColumnName("transaction_ref");
        entity.HasOne(d => d.Cashier).WithMany(p => p.Payments).HasForeignKey(d => d.CashierId).HasConstraintName("payments_cashier_id_fkey");
        entity.HasOne(d => d.Order).WithMany(p => p.Payments).HasForeignKey(d => d.OrderId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("payments_order_id_fkey");
    }
}
