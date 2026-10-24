using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class RentalConfiguration : IEntityTypeConfiguration<Rental>
{
    public void Configure(EntityTypeBuilder<Rental> entity)
    {
        entity.HasKey(e => e.Id).HasName("rentals_pkey");
        entity.ToTable("rentals", "smart_pool", table =>
            table.HasCheckConstraint("ck_rentals_issued_snapshot", "fee_amount IS NULL OR (fee_amount >= 0 AND deposit_amount = 20000 AND product_name_snapshot IS NOT NULL)"));
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.DepositAmount).HasPrecision(18, 2).HasDefaultValueSql("0").HasColumnName("deposit_amount");
        entity.Property(e => e.FeeAmount).HasPrecision(18, 2).HasColumnName("fee_amount");
        entity.Property(e => e.ProductNameSnapshot).HasMaxLength(255).HasColumnName("product_name_snapshot");
        entity.Property(e => e.SettlementId).HasColumnName("settlement_id");
        entity.Property(e => e.ResolvedAtUtc).HasColumnType("timestamp with time zone").HasColumnName("resolved_at_utc");
        entity.HasOne<RentalTransaction>().WithMany().HasForeignKey(e => e.SettlementId).OnDelete(DeleteBehavior.Restrict);
        entity.Property(e => e.OrderId).HasColumnName("order_id");
        entity.Property(e => e.ProductId).HasColumnName("product_id");
        entity.Property(e => e.RentTime).HasDefaultValueSql("now()").HasColumnName("rent_time");
        entity.Property(e => e.ReturnTime).HasColumnName("return_time");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Renting'::character varying").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.HasOne(d => d.Order).WithMany(p => p.Rentals).HasForeignKey(d => d.OrderId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("rentals_order_id_fkey");
        entity.HasOne(d => d.Product).WithMany(p => p.Rentals).HasForeignKey(d => d.ProductId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("rentals_product_id_fkey");
    }
}
