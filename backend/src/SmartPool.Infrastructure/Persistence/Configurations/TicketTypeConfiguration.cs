using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class TicketTypeConfiguration : IEntityTypeConfiguration<TicketType>
{
    public void Configure(EntityTypeBuilder<TicketType> entity)
    {
        entity.HasKey(e => e.Id).HasName("ticket_types_pkey");
        entity.ToTable("ticket_types", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.DurationDays).HasColumnName("duration_days");
        entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");
        entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
        entity.Property(e => e.Price).HasPrecision(18, 2).HasColumnName("price");
        entity.Property(e => e.TicketCategory).HasMaxLength(50).HasColumnName("ticket_category");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
    }
}
