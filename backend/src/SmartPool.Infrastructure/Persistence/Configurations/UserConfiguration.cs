using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(e => e.Id).HasName("users_pkey");
        entity.ToTable("users", "smart_pool");
        entity.HasIndex(e => e.Email, "users_email_key").IsUnique();
        entity.HasIndex(e => e.Phone, "users_phone_key").IsUnique();
        entity.HasIndex(e => e.Username, "users_username_key").IsUnique();
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.Email).HasMaxLength(150).HasColumnName("email");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");
        entity.Property(e => e.PasswordHash).HasMaxLength(255).HasColumnName("password_hash");
        entity.Property(e => e.Phone).HasMaxLength(20).HasColumnName("phone");
        entity.Property(e => e.RoleId).HasColumnName("role_id");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Active'::character varying").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.Property(e => e.Username).HasMaxLength(100).HasColumnName("username");
        entity.HasOne(d => d.Role).WithMany(p => p.Users).HasForeignKey(d => d.RoleId).HasConstraintName("users_role_id_fkey");
    }
}
