using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services;

public sealed partial class ServiceOperations
{
    public async Task<RentalPaymentStatusResponse> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var intent = await LockRentalPaymentAsync(id, cancellationToken);
        await ExpireRentalPaymentAsync(intent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        var result = await BuildRentalPaymentStatusAsync(intent, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<PagedResponse<RentalPaymentStatusResponse>> ListPaymentsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        if (pageIndex < 1 || pageSize is < 1 or > 100)
            throw new ValidationException([new ValidationFailure("PageSize", "Chọn trang hợp lệ và từ 1 đến 100 giao dịch mỗi trang.")]);
        await ExpirePaymentsAsync(cancellationToken);
        var query = _context.RentalPaymentIntents.AsNoTracking().Where(intent =>
            intent.Status == RentalPaymentPolicy.Pending || intent.Status == RentalPaymentPolicy.Partial || intent.Status == RentalPaymentPolicy.Paid
            || _context.BankTransferReceipts.Any(receipt => receipt.OrderId == intent.OrderId && receipt.NeedsReconciliation));
        var total = await query.CountAsync(cancellationToken);
        var intents = await query.OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = new List<RentalPaymentStatusResponse>();
        foreach (var intent in intents) items.Add(await BuildRentalPaymentStatusAsync(intent, cancellationToken));
        return new() { Items = items, TotalCount = total, PageIndex = pageIndex, PageSize = pageSize };
    }

    public async Task<RentalPaymentStatusResponse> CancelPaymentAsync(Guid id, Guid operatorId, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var intent = await LockRentalPaymentAsync(id, cancellationToken);
        await ExpireRentalPaymentAsync(intent, cancellationToken);
        if (intent.Status is RentalPaymentPolicy.Paid or RentalPaymentPolicy.Issued)
            throw new ServiceConflictException("Giao dịch đã nhận đủ tiền. Không tự hủy hoặc tự hoàn tiền; hãy xử lý tại quầy.");
        if (RentalPaymentPolicy.IsWaiting(intent.Status))
            await ReleaseRentalReservationAsync(intent, RentalPaymentPolicy.Cancelled, operatorId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        var result = await BuildRentalPaymentStatusAsync(intent, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task ExpirePaymentsAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var ids = await _context.RentalPaymentIntents.AsNoTracking()
            .Where(item => (item.Status == RentalPaymentPolicy.Pending || item.Status == RentalPaymentPolicy.Partial) && item.ExpiresAtUtc <= now)
            .OrderBy(item => item.ExpiresAtUtc).Select(item => item.Id).Take(100).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var intent = await LockRentalPaymentAsync(id, cancellationToken);
            await ExpireRentalPaymentAsync(intent, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private async Task<RentalPaymentIntent> LockRentalPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        var orderId = await _context.RentalPaymentIntents.AsNoTracking().Where(item => item.Id == id)
            .Select(item => (Guid?)item.OrderId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy giao dịch QR cho thuê.");
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM smart_pool.orders WHERE id = {orderId} FOR UPDATE", cancellationToken);
        var tracked = _context.RentalPaymentIntents.Local.FirstOrDefault(item => item.Id == id);
        if (tracked is not null)
        {
            await _context.Entry(tracked).ReloadAsync(cancellationToken);
            return tracked;
        }
        return await _context.RentalPaymentIntents.SingleAsync(item => item.Id == id, cancellationToken);
    }

    private async Task ExpireRentalPaymentAsync(RentalPaymentIntent intent, CancellationToken cancellationToken)
    {
        if (RentalPaymentPolicy.HasExpired(intent.Status, intent.ExpiresAtUtc, _clock.GetUtcNow().UtcDateTime))
            await ReleaseRentalReservationAsync(intent, RentalPaymentPolicy.Expired, intent.InitiatedByUserId, cancellationToken);
    }

    private async Task ReleaseRentalReservationAsync(RentalPaymentIntent intent, string status, Guid actorId, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var line in ReadRentalPaymentLines(intent).OrderBy(item => item.ProductId))
        {
            var product = await _context.Products.FromSqlInterpolated($"SELECT * FROM smart_pool.products WHERE id = {line.ProductId} FOR UPDATE")
                .AsNoTracking().SingleAsync(cancellationToken);
            if (product.StockQuantity is null || product.StockQuantity > int.MaxValue - line.Quantity)
                throw new ServiceConflictException("Không thể trả lại số lượng đang giữ. Hãy liên hệ quản trị.");
            await _context.Products.Where(item => item.Id == line.ProductId).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.StockQuantity, product.StockQuantity.Value + line.Quantity)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            _context.InventoryLogs.Add(new InventoryLog
            {
                Id = Guid.NewGuid(), ProductId = line.ProductId, ChangeType = "RentalReservationRelease", Quantity = line.Quantity,
                Note = $"Rental QR {status}: {intent.Id}", CreatedBy = actorId, CreatedAt = now
            });
        }
        intent.Status = status;
        await _context.Orders.Where(item => item.Id == intent.OrderId).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Status, "Cancelled").SetProperty(item => item.UpdatedAt, now), cancellationToken);
        await _context.BankTransferReceipts.Where(item => item.OrderId == intent.OrderId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.NeedsReconciliation, true), cancellationToken);
    }

    private async Task<RentalPaymentStatusResponse> BuildRentalPaymentStatusAsync(RentalPaymentIntent intent, CancellationToken cancellationToken)
    {
        var customer = await _context.Orders.AsNoTracking().Where(item => item.Id == intent.OrderId)
            .Select(item => new { item.CustomerName, item.CustomerPhone }).SingleAsync(cancellationToken);
        var receipts = await _context.BankTransferReceipts.AsNoTracking().Where(item => item.OrderId == intent.OrderId).ToListAsync(cancellationToken);
        var received = receipts.Sum(item => item.Amount);
        var due = intent.FeeAmount + intent.DepositAmount;
        CheckoutRentalResponse? issued = null;
        if (intent.Status == RentalPaymentPolicy.Issued)
        {
            var json = await _context.RentalTransactions.AsNoTracking().Where(item => item.OrderId == intent.OrderId && item.Kind == "Collection")
                .Select(item => item.ResultJson).SingleAsync(cancellationToken);
            issued = JsonSerializer.Deserialize<CheckoutRentalResponse>(json);
        }
        return new RentalPaymentStatusResponse
        {
            Id = intent.Id, OrderId = intent.OrderId, TransactionRef = intent.Reference, Status = intent.Status,
            CustomerName = customer.CustomerName, CustomerPhone = customer.CustomerPhone,
            FeeAmount = intent.FeeAmount, DepositAmount = intent.DepositAmount, TotalToCollect = due, PaidAmount = received,
            RemainingAmount = RentalPaymentPolicy.Remaining(due, received), ExcessAmount = RentalPaymentPolicy.Excess(due, received),
            RequiresReconciliation = receipts.Any(item => item.NeedsReconciliation) || received > due,
            CreatedAtUtc = intent.CreatedAtUtc, ExpiresAtUtc = intent.ExpiresAtUtc, ServerTimeUtc = _clock.GetUtcNow().UtcDateTime,
            BankId = _paymentOptions.BankId, AccountNumber = _paymentOptions.AccountNumber, AccountName = _paymentOptions.AccountName,
            Items = ReadRentalPaymentLines(intent).Select(line => new RentalPaymentLine
            {
                ProductId = line.ProductId, Name = line.Name, Quantity = line.Quantity, UnitPrice = line.UnitPrice
            }).ToList(), IssuedResult = issued
        };
    }
}
