using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("refresh_tokens");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.Property(e => e.TokenHash).HasColumnName("token_hash");
        entity.Property(e => e.ExpiredAt).HasColumnName("expired_at");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        entity.Property(e => e.RevokeAt).HasColumnName("revoke_at");
        // Indexes
        entity.HasIndex(e => e.UserId).HasDatabaseName("idx_refresh_tokens_user_id");
        entity.HasIndex(e => e.TokenHash).HasDatabaseName("idx_refresh_tokens_token_hash");
        // Khoá ngoại
        entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
