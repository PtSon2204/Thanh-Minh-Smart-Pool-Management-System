using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> entity)
    {
        entity.HasKey(e => e.Id).HasName("orders_pkey");
        entity.ToTable("orders", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.CustomerName).HasMaxLength(255).HasColumnName("customer_name");
        entity.Property(e => e.CustomerPhone).HasMaxLength(20).HasColumnName("customer_phone");
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2).HasDefaultValueSql("0").HasColumnName("discount_amount");
        entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
        entity.Property(e => e.FinalAmount).HasPrecision(18, 2).HasColumnName("final_amount");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Pending'::character varying").HasColumnName("status");
        entity.Property(e => e.TotalAmount).HasPrecision(18, 2).HasColumnName("total_amount");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.Property(e => e.VoucherId).HasColumnName("voucher_id");
        entity.HasOne(d => d.Employee).WithMany(p => p.Orders).HasForeignKey(d => d.EmployeeId).HasConstraintName("orders_employee_id_fkey");
        entity.HasOne(d => d.User).WithMany(p => p.Orders).HasForeignKey(d => d.UserId).HasConstraintName("orders_user_id_fkey");
        entity.HasOne(d => d.Voucher).WithMany(p => p.Orders).HasForeignKey(d => d.VoucherId).HasConstraintName("orders_voucher_id_fkey");
    }
}
