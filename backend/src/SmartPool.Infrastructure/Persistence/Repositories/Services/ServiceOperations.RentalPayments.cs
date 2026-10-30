using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services;

public sealed partial class ServiceOperations
{
    private sealed record RentalQrLine(Guid ProductId, string Name, int Quantity, decimal UnitPrice);

    public async Task<RentalPaymentStatusResponse> CreateQrAsync(CreateRentalQrCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_paymentOptions.ApiKey) || string.IsNullOrWhiteSpace(_paymentOptions.AccountNumber)
            || string.IsNullOrWhiteSpace(_paymentOptions.BankId))
            throw new ServiceConflictException("Thanh toán QR chưa được cấu hình. Hãy dùng tiền mặt hoặc liên hệ quản trị.");

        await new CheckoutRentalValidator().ValidateAndThrowAsync(new CheckoutRentalCommand
        {
            RequestId = command.RequestId, OperatorId = command.OperatorId, CustomerName = command.CustomerName,
            CustomerPhone = command.CustomerPhone, Items = command.Items, CollectionConfirmed = true
        }, cancellationToken);
        var hash = RentalRequestHash(new
        {
            Method = "TRANSFER", command.OperatorId, CustomerName = command.CustomerName!.Trim(),
            CustomerPhone = command.CustomerPhone!.Trim(),
            Items = command.Items.OrderBy(item => item.ProductId).Select(item => new { item.ProductId, item.Quantity, item.ExpectedUnitPrice }).ToArray()
        });
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await LockRentalRequestAsync(command.RequestId, cancellationToken);
        var existing = await _context.RentalPaymentIntents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == command.RequestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != hash || existing.InitiatedByUserId != command.OperatorId)
                throw new ServiceConflictException("Mã yêu cầu đã được dùng với thông tin khác. Hãy tiếp tục giao dịch đã tạo.");
            var replay = await BuildRentalPaymentStatusAsync(existing, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }
        if (await _context.RentalTransactions.AnyAsync(item => item.Id == command.RequestId, cancellationToken))
            throw new ServiceConflictException("Mã yêu cầu đã thuộc một giao dịch cho thuê khác.");

        var lines = new List<RentalQrLine>();
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var item in command.Items.OrderBy(item => item.ProductId))
        {
            var product = await _context.Products.FromSqlInterpolated($"SELECT * FROM smart_pool.products WHERE id = {item.ProductId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm cho thuê.");
            EnsureRentalAvailability(product, item.Quantity);
            if (product.Price != item.ExpectedUnitPrice)
                throw new ServiceConflictException("Giá thuê đã thay đổi. Hãy lấy báo giá mới.");
            lines.Add(new(product.Id, product.Name, item.Quantity, product.Price));
        }
        var fee = lines.Sum(line => line.UnitPrice * line.Quantity);
        var deposit = lines.Sum(line => line.Quantity) * RentalDepositPerUnit;
        if (!RentalPaymentPolicy.IsWholeVnd(fee + deposit))
            throw new ValidationException([new ValidationFailure("Items", "Tổng tiền QR phải là số nguyên VND. Không tự làm tròn báo giá.")]);

        var employeeExists = await _context.Employees.AnyAsync(item => item.UserId == command.OperatorId, cancellationToken);
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = null, EmployeeId = employeeExists ? command.OperatorId : null,
            CustomerName = command.CustomerName!.Trim(), CustomerPhone = command.CustomerPhone!.Trim(),
            TotalAmount = fee, FinalAmount = fee, DiscountAmount = 0, Status = "RentalPending", IsDeleted = false,
            CreatedAt = now, UpdatedAt = now
        };
        var intent = new RentalPaymentIntent
        {
            Id = command.RequestId, OrderId = order.Id, RequestHash = hash, Reference = $"RN{command.RequestId:N}".ToUpperInvariant(),
            Status = RentalPaymentPolicy.Pending, FeeAmount = fee, DepositAmount = deposit,
            LinesJson = JsonSerializer.Serialize(lines), InitiatedByUserId = command.OperatorId,
            CreatedAtUtc = now, ExpiresAtUtc = now.Add(RentalPaymentPolicy.Lifetime)
        };
        _context.Orders.Add(order);
        _context.RentalPaymentIntents.Add(intent);
        foreach (var line in lines)
        {
            await _context.Products.Where(item => item.Id == line.ProductId).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.StockQuantity, item => item.StockQuantity - line.Quantity)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            _context.OrderDetails.Add(new OrderDetail
            {
                Id = Guid.NewGuid(), OrderId = order.Id, ItemType = "Product", ItemId = line.ProductId,
                Quantity = line.Quantity, UnitPrice = line.UnitPrice, CreatedAt = now
            });
            _context.InventoryLogs.Add(new InventoryLog
            {
                Id = Guid.NewGuid(), ProductId = line.ProductId, ChangeType = "RentalReservation", Quantity = -line.Quantity,
                Note = $"Rental QR reservation {intent.Id}", CreatedBy = command.OperatorId, CreatedAt = now
            });
        }
        await _context.SaveChangesAsync(cancellationToken);
        var response = await BuildRentalPaymentStatusAsync(intent, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private static List<RentalQrLine> ReadRentalPaymentLines(RentalPaymentIntent intent) =>
        JsonSerializer.Deserialize<List<RentalQrLine>>(intent.LinesJson)
        ?? throw new InvalidOperationException("Stored rental payment snapshot is invalid.");
}
