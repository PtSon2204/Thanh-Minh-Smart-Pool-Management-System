using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.HasKey(e => e.UserId).HasName("employees_pkey");
        entity.ToTable("employees", "smart_pool");
        entity.Property(e => e.UserId).ValueGeneratedNever().HasColumnName("user_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.JoinDate).HasColumnName("join_date");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Working'::character varying").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.HasOne(d => d.User).WithOne(p => p.Employee).HasForeignKey<Employee>(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("employees_user_id_fkey");
    }
}
