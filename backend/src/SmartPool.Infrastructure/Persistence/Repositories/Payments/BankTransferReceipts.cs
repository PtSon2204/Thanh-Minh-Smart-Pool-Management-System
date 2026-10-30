using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Payments;

internal static class BankTransferReceipts
{
    public static Task<int> LockAsync(SmartPoolDbContext context, long eventId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"SePay:{eventId}"}, 0))", cancellationToken);

    public static async Task<(BankTransferReceipt Receipt, bool IsNew)> RegisterAsync(SmartPoolDbContext context,
        BankTransferEvent transfer, Guid? orderId, string disposition, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var hash = RentalPaymentPolicy.FinancialHash(transfer);
        var existing = await context.BankTransferReceipts.SingleOrDefaultAsync(
            item => item.Provider == RentalPaymentPolicy.Provider && item.ProviderEventId == transfer.EventId, cancellationToken);
        if (existing is not null)
        {
            if (existing.FinancialHash != hash)
            {
                existing.NeedsReconciliation = true;
            }
            return (existing, false);
        }

        var receipt = new BankTransferReceipt
        {
            Id = Guid.NewGuid(), Provider = RentalPaymentPolicy.Provider, ProviderEventId = transfer.EventId,
            FinancialHash = hash, BankReference = transfer.BankReferenceCode, AccountNumber = transfer.AccountNumber,
            Direction = "in", Amount = transfer.Amount, Reference = transfer.Reference, OrderId = orderId,
            ReceivedAtUtc = nowUtc, Disposition = disposition, NeedsReconciliation = disposition != "RENTAL_APPLIED" && disposition != "TICKET_APPLIED"
        };
        context.BankTransferReceipts.Add(receipt);
        return (receipt, true);
    }
}
