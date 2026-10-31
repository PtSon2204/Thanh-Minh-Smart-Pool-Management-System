using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class BankTransferReceiptConfiguration : IEntityTypeConfiguration<BankTransferReceipt>
{
    public void Configure(EntityTypeBuilder<BankTransferReceipt> entity)
    {
        entity.ToTable("bank_transfer_receipts", "smart_pool", table =>
        {
            table.HasCheckConstraint("ck_bank_transfer_receipts_amount", "amount > 0");
            table.HasCheckConstraint("ck_bank_transfer_receipts_direction", "direction = 'in'");
        });
        entity.HasKey(item => item.Id).HasName("bank_transfer_receipts_pkey");
        entity.Property(item => item.Id).ValueGeneratedNever().HasColumnName("id");
        entity.Property(item => item.Provider).HasMaxLength(50).IsRequired().HasColumnName("provider");
        entity.Property(item => item.ProviderEventId).HasColumnName("provider_event_id");
        entity.Property(item => item.FinancialHash).HasMaxLength(64).IsRequired().HasColumnName("financial_hash");
        entity.Property(item => item.AccountNumber).HasMaxLength(50).IsRequired().HasColumnName("account_number");
        entity.Property(item => item.Direction).HasMaxLength(20).IsRequired().HasColumnName("direction");
        entity.Property(item => item.Reference).HasMaxLength(64).IsRequired().HasColumnName("reference");
        entity.Property(item => item.BankReference).HasMaxLength(128).HasColumnName("bank_reference");
        entity.Property(item => item.Disposition).HasMaxLength(30).IsRequired().HasColumnName("disposition");
        entity.Property(item => item.Amount).HasPrecision(18, 2).HasColumnName("amount");
        entity.Property(item => item.OrderId).HasColumnName("order_id");
        entity.Property(item => item.ReceivedAtUtc).HasColumnType("timestamp with time zone").HasColumnName("received_at_utc");
        entity.Property(item => item.NeedsReconciliation).HasColumnName("needs_reconciliation");
        entity.HasIndex(item => new { item.Provider, item.ProviderEventId }).IsUnique().HasDatabaseName("ux_bank_transfer_receipts_provider_event");
        entity.HasIndex(item => item.Reference).HasDatabaseName("ix_bank_transfer_receipts_reference");
        entity.HasIndex(item => item.FinancialHash).HasDatabaseName("ix_bank_transfer_receipts_financial_hash");
        entity.HasOne<Order>().WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
