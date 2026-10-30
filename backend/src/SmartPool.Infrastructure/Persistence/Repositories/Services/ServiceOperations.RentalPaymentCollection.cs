using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.Repositories.Payments;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services;

public sealed partial class ServiceOperations
{
    public async Task<BankTransferResult> ApplyTransferAsync(BankTransferEvent transfer, CancellationToken cancellationToken)
    {
        if (transfer.EventId <= 0 || !RentalPaymentPolicy.IsWholeVnd(transfer.Amount)
            || transfer.AccountNumber != _paymentOptions.AccountNumber || !RentalPaymentPolicy.IsRentalReference(transfer.Reference))
            return new(false, "Thông tin giao dịch ngân hàng không hợp lệ.");
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await BankTransferReceipts.LockAsync(_context, transfer.EventId, cancellationToken);
        var id = await _context.RentalPaymentIntents.AsNoTracking().Where(item => item.Reference == transfer.Reference)
            .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (id is null)
        {
            await BankTransferReceipts.RegisterAsync(_context, transfer, null, "UNMATCHED", _clock.GetUtcNow().UtcDateTime, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, "Đã ghi nhận khoản chuyển chưa khớp đơn để đối soát.");
        }
        var intent = await LockRentalPaymentAsync(id.Value, cancellationToken);
        await ExpireRentalPaymentAsync(intent, cancellationToken);
        var eligible = RentalPaymentPolicy.IsWaiting(intent.Status);
        var registration = await BankTransferReceipts.RegisterAsync(_context, transfer, intent.OrderId,
            eligible ? "RENTAL_APPLIED" : "RECONCILIATION", _clock.GetUtcNow().UtcDateTime, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (registration.IsNew && eligible)
        {
            var received = await _context.BankTransferReceipts.Where(item => item.OrderId == intent.OrderId && item.Disposition == "RENTAL_APPLIED")
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0;
            var due = intent.FeeAmount + intent.DepositAmount;
            intent.Status = received >= due ? RentalPaymentPolicy.Paid : RentalPaymentPolicy.Partial;
            if (intent.Status == RentalPaymentPolicy.Paid) intent.PaidAtUtc = _clock.GetUtcNow().UtcDateTime;
            if (received > due) registration.Receipt.NeedsReconciliation = true;
            await _context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(true, registration.IsNew ? "Đã ghi nhận chuyển khoản." : "Giao dịch đã được ghi nhận; không thu tiền lần nữa.");
    }

    public async Task<CheckoutRentalResponse> IssuePaymentAsync(Guid id, Guid operatorId, CancellationToken cancellationToken)
    {
        if (operatorId == Guid.Empty)
            throw new ValidationException([new ValidationFailure("OperatorId", "Không xác định được nhân viên xuất đồ.")]);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var intent = await LockRentalPaymentAsync(id, cancellationToken);
        if (intent.Status == RentalPaymentPolicy.Issued)
        {
            var saved = await _context.RentalTransactions.AsNoTracking().Where(item => item.OrderId == intent.OrderId && item.Kind == "Collection")
                .Select(item => item.ResultJson).SingleAsync(cancellationToken);
            var replay = JsonSerializer.Deserialize<CheckoutRentalResponse>(saved)
                ?? throw new InvalidOperationException("Stored rental issue outcome is invalid.");
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }
        await ExpireRentalPaymentAsync(intent, cancellationToken);
        if (intent.Status != RentalPaymentPolicy.Paid)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new ServiceConflictException("Chưa xác nhận đủ tiền hoặc giao dịch đã hết hạn. Không được xuất đồ.");
        }
        var received = await _context.BankTransferReceipts.Where(item => item.OrderId == intent.OrderId && item.Disposition == "RENTAL_APPLIED")
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0;
        if (received < intent.FeeAmount + intent.DepositAmount)
            throw new ServiceConflictException("Chưa có đủ bằng chứng nhận tiền. Hãy kiểm tra trạng thái thanh toán.");

        var lines = ReadRentalPaymentLines(intent);
        foreach (var line in lines.OrderBy(item => item.ProductId))
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM smart_pool.products WHERE id = {line.ProductId} FOR UPDATE", cancellationToken);
        var now = _clock.GetUtcNow().UtcDateTime;
        var employeeExists = await _context.Employees.AnyAsync(item => item.UserId == operatorId, cancellationToken);
        var payment = new Payment
        {
            Id = Guid.NewGuid(), OrderId = intent.OrderId, Amount = intent.FeeAmount, PaymentMethod = "TRANSFER",
            Status = "COMPLETED", CashierId = employeeExists ? operatorId : null,
            TransactionRef = intent.Reference, PaymentTime = intent.PaidAtUtc ?? now, CreatedAt = now
        };
        var rentals = new List<Rental>();
        foreach (var line in lines)
        {
            rentals.AddRange(Enumerable.Range(0, line.Quantity).Select(_ => new Rental
            {
                Id = Guid.NewGuid(), OrderId = intent.OrderId, ProductId = line.ProductId, ProductNameSnapshot = line.Name,
                FeeAmount = line.UnitPrice, DepositAmount = RentalDepositPerUnit, RentTime = now,
                Status = "Renting", UpdatedAt = now
            }));
        }
        var result = new CheckoutRentalResponse
        {
            OrderId = intent.OrderId, RequestId = intent.Id, OrderStatus = "Renting", TotalAmount = intent.FeeAmount,
            FinalAmount = intent.FeeAmount, RentalFeeTotal = intent.FeeAmount, DepositTotal = intent.DepositAmount,
            TotalCollected = intent.FeeAmount + intent.DepositAmount,
            Rentals = rentals.Select(rental => new CheckoutRentalItemResponse
            {
                Id = rental.Id, OrderId = rental.OrderId, ProductId = rental.ProductId, ProductName = rental.ProductNameSnapshot!,
                RentTime = rental.RentTime, DepositAmount = rental.DepositAmount, FeeAmount = rental.FeeAmount,
                Status = "Renting", OrderStatus = "Renting"
            }).ToList()
        };
        _context.Payments.Add(payment);
        _context.Rentals.AddRange(rentals);
        _context.RentalTransactions.Add(new RentalTransaction
        {
            Id = intent.Id, OrderId = intent.OrderId, PaymentId = payment.Id, Kind = "Collection", RequestHash = intent.RequestHash,
            FeeAmount = intent.FeeAmount, DepositAmount = intent.DepositAmount, ConfirmedByUserId = operatorId,
            ConfirmedAtUtc = now, ResultJson = JsonSerializer.Serialize(result)
        });
        intent.Status = RentalPaymentPolicy.Issued;
        intent.IssuedByUserId = operatorId;
        intent.IssuedAtUtc = now;
        await _context.Orders.Where(item => item.Id == intent.OrderId).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Status, "Renting").SetProperty(item => item.UpdatedAt, now), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
