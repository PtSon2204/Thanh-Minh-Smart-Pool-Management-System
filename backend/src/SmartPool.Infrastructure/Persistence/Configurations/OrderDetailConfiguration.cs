using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> entity)
    {
        entity.HasKey(e => e.Id).HasName("order_details_pkey");
        entity.ToTable("order_details", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.ItemId).HasColumnName("item_id");
        entity.Property(e => e.ItemType).HasMaxLength(50).HasColumnName("item_type");
        entity.Property(e => e.OrderId).HasColumnName("order_id");
        entity.Property(e => e.Quantity).HasColumnName("quantity");
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2).HasColumnName("unit_price");
        entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails).HasForeignKey(d => d.OrderId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("order_details_order_id_fkey");
    }
}
