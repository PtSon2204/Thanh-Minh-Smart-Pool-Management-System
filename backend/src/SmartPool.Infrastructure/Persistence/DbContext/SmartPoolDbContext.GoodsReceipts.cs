using Microsoft.EntityFrameworkCore;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.DbContext;

public partial class SmartPoolDbContext
{
    public DbSet<GoodsReceipt> GoodsReceipts { get; set; }
    public DbSet<GoodsReceiptDetail> GoodsReceiptDetails { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.ToTable("goods_receipts", "smart_pool", table =>
                table.HasCheckConstraint("ck_goods_receipts_status", "status IN ('Draft', 'Posted', 'Cancelled')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.ReceiptCode).HasColumnName("receipt_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ReceivedAt).HasColumnName("received_at");
            entity.Property(e => e.SupplierName).HasColumnName("supplier_name").HasMaxLength(255);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Draft");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(e => e.ReceiptCode).IsUnique().HasDatabaseName("ux_goods_receipts_code");
            entity.HasIndex(e => e.ReceivedAt).IsDescending().HasDatabaseName("ix_goods_receipts_received_at");
            entity.HasOne(e => e.Creator).WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsReceiptDetail>(entity =>
        {
            entity.ToTable("goods_receipt_details", "smart_pool", table =>
            {
                table.HasCheckConstraint("ck_goods_receipt_details_quantity", "quantity > 0");
                table.HasCheckConstraint("ck_goods_receipt_details_cost", "unit_cost >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.ReceiptId).HasColumnName("receipt_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 2);
            entity.HasIndex(e => new { e.ReceiptId, e.ProductId }).IsUnique();
            entity.HasOne(e => e.Receipt).WithMany(e => e.Details).HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryLog>(entity =>
        {
            entity.Property(e => e.ReceiptDetailId).HasColumnName("receipt_detail_id");
            entity.HasIndex(e => e.ReceiptDetailId).IsUnique().HasDatabaseName("ux_inventory_logs_receipt_detail_id");
            entity.HasOne(e => e.ReceiptDetail).WithMany().HasForeignKey(e => e.ReceiptDetailId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
