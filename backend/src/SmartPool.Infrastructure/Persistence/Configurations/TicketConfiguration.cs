using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> entity)
    {
        entity.HasKey(e => e.Id).HasName("tickets_pkey");
        entity.ToTable("tickets", "smart_pool");
        entity.HasIndex(e => e.QrCode, "tickets_qr_code_key").IsUnique();
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.ExpiryDate).HasColumnName("expiry_date");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");
        entity.Property(e => e.IssueDate).HasDefaultValueSql("now()").HasColumnName("issue_date");
        entity.Property(e => e.QrCode).HasMaxLength(255).HasColumnName("qr_code");
        entity.Ignore(e => e.RemainingEntries); // Cột này chưa tồn tại trong DB
        entity.Property(e => e.RowVersion).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("row_version");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Active'::character varying").HasColumnName("status");
        entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.HasOne(d => d.TicketType).WithMany(p => p.Tickets).HasForeignKey(d => d.TicketTypeId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("tickets_ticket_type_id_fkey");
        entity.HasOne(d => d.User).WithMany(p => p.Tickets).HasForeignKey(d => d.UserId).HasConstraintName("tickets_user_id_fkey");
    }
}
