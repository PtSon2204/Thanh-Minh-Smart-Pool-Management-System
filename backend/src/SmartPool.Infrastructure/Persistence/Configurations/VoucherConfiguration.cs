using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> entity)
    {
        entity.HasKey(e => e.Id).HasName("vouchers_pkey");
        entity.ToTable("vouchers", "smart_pool");
        entity.HasIndex(e => e.Code, "vouchers_code_key").IsUnique();
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.Code).HasMaxLength(50).HasColumnName("code");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.DiscountType).HasMaxLength(20).HasColumnName("discount_type");
        entity.Property(e => e.DiscountValue).HasPrecision(18, 2).HasColumnName("discount_value");
        entity.Property(e => e.EndDate).HasColumnName("end_date");
        entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");
        entity.Property(e => e.MinOrderValue).HasPrecision(18, 2).HasDefaultValueSql("0").HasColumnName("min_order_value");
        entity.Property(e => e.StartDate).HasColumnName("start_date");
    }
}
