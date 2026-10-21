using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> entity)
    {
        entity.HasKey(e => e.UserId).HasName("user_profiles_pkey");
        entity.ToTable("user_profiles", "smart_pool");
        entity.Property(e => e.UserId).ValueGeneratedNever().HasColumnName("user_id");
        entity.Property(e => e.Address).HasColumnName("address");
        entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
        entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
        entity.Property(e => e.FullName).HasMaxLength(255).HasColumnName("full_name");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.HasOne(d => d.User).WithOne(p => p.UserProfile).HasForeignKey<UserProfile>(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("user_profiles_user_id_fkey");
    }
}
