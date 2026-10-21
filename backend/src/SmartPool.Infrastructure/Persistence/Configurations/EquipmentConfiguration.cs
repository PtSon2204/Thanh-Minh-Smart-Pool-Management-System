using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> entity)
    {
        entity.HasKey(e => e.Id).HasName("equipments_pkey");
        entity.ToTable("equipments", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.Category).HasMaxLength(50).HasColumnName("category");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");
        entity.Property(e => e.Location).HasMaxLength(255).HasColumnName("location");
        entity.Property(e => e.Metadata).HasColumnType("jsonb").HasColumnName("metadata");
        entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Active'::character varying").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
    }
}
