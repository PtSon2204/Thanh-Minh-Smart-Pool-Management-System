using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.HasKey(e => e.Id).HasName("notifications_pkey");
        entity.ToTable("notifications", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.Content).HasColumnName("content");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.IsRead).HasDefaultValue(false).HasColumnName("is_read");
        entity.Property(e => e.Title).HasMaxLength(255).HasColumnName("title");
        entity.Property(e => e.Type).HasMaxLength(50).HasColumnName("type");
        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.HasOne(d => d.User).WithMany(p => p.Notifications).HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("notifications_user_id_fkey");
    }
}
