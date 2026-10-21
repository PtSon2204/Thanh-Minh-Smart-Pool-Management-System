using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class EntryLogConfiguration : IEntityTypeConfiguration<EntryLog>
{
    public void Configure(EntityTypeBuilder<EntryLog> entity)
    {
        entity.HasKey(e => e.Id).HasName("entry_logs_pkey");
        entity.ToTable("entry_logs", "smart_pool");
        entity.HasIndex(e => e.ScanTime, "ix_entry_logs_scan_time");
        entity.HasIndex(e => new { e.TicketId, e.ScanTime }, "ix_entry_logs_ticket_id_scan_time");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.GateId).HasMaxLength(50).HasColumnName("gate_id");
        entity.Property(e => e.InputMode).HasMaxLength(50).HasColumnName("input_mode");
        entity.Property(e => e.Message).HasColumnName("message");
        entity.Property(e => e.OperatorId).HasColumnName("operator_id");
        entity.Property(e => e.ScanTime).HasDefaultValueSql("now()").HasColumnName("scan_time");
        entity.Property(e => e.Status).HasMaxLength(50).HasColumnName("status");
        entity.Property(e => e.TicketId).HasColumnName("ticket_id");
        entity.HasOne(d => d.Ticket).WithMany(p => p.EntryLogs).HasForeignKey(d => d.TicketId).HasConstraintName("entry_logs_ticket_id_fkey");
        entity.HasOne(d => d.Operator).WithMany().HasForeignKey(d => d.OperatorId).HasConstraintName("entry_logs_operator_id_fkey");
    }
}
