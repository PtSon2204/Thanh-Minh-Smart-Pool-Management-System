using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class InventoryLogConfiguration : IEntityTypeConfiguration<InventoryLog>
{
    public void Configure(EntityTypeBuilder<InventoryLog> entity)
    {
        entity.HasKey(e => e.Id).HasName("inventory_logs_pkey");
        entity.ToTable("inventory_logs", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.ChangeType).HasMaxLength(50).HasColumnName("change_type");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.Note).HasColumnName("note");
        entity.Property(e => e.ProductId).HasColumnName("product_id");
        entity.Property(e => e.Quantity).HasColumnName("quantity");
        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InventoryLogs).HasForeignKey(d => d.CreatedBy).HasConstraintName("inventory_logs_created_by_fkey");
        entity.HasOne(d => d.Product).WithMany(p => p.InventoryLogs).HasForeignKey(d => d.ProductId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("inventory_logs_product_id_fkey");
    }
}
