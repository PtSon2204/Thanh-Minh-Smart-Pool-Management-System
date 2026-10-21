using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> entity)
    {
        entity.HasKey(e => e.Id).HasName("shifts_pkey");
        entity.ToTable("shifts", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.EndTime).HasColumnName("end_time");
        entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
        entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
        entity.Property(e => e.StartTime).HasColumnName("start_time");
    }
}
