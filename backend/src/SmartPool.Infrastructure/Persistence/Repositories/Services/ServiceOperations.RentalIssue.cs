using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.QuoteRental;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services;

public sealed partial class ServiceOperations
{
    private const decimal RentalDepositPerUnit = 20000m;

    public async Task<QuoteRentalResponse> QuoteRentalAsync(QuoteRentalCommand command, CancellationToken cancellationToken)
    {
        await new QuoteRentalValidator().ValidateAndThrowAsync(command, cancellationToken);
        var ids = command.Items.Select(item => item.ProductId).ToList();
        var products = await _context.Products.AsNoTracking().Where(product => ids.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var result = new QuoteRentalResponse();
        foreach (var item in command.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product)) throw new KeyNotFoundException("Không tìm thấy sản phẩm cho thuê.");
            EnsureRentalAvailability(product, item.Quantity);
            result.Items.Add(new QuotedRentalLine { ProductId = product.Id, ProductName = product.Name, Quantity = item.Quantity, UnitPrice = product.Price, RentalFee = product.Price * item.Quantity, DepositAmount = RentalDepositPerUnit * item.Quantity });
        }
        result.TotalQuantity = command.Items.Sum(item => item.Quantity);
        result.RentalFeeTotal = result.Items.Sum(item => item.RentalFee);
        result.DepositTotal = RentalDepositPerUnit * result.TotalQuantity;
        result.TotalToCollect = result.RentalFeeTotal + result.DepositTotal;
        return result;
    }

    public async Task<CheckoutRentalResponse> CheckoutRentalAsync(CheckoutRentalCommand command, CancellationToken cancellationToken)
    {
        await new CheckoutRentalValidator().ValidateAndThrowAsync(command, cancellationToken);
        var hash = RentalRequestHash(new { Operation = "Collection", command.OperatorId, CustomerName = command.CustomerName!.Trim(), CustomerPhone = command.CustomerPhone!.Trim(), command.CollectionConfirmed,
            Items = command.Items.OrderBy(item => item.ProductId).Select(item => new { item.ProductId, item.Quantity, item.ExpectedUnitPrice }).ToArray() });
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await LockRentalRequestAsync(command.RequestId, cancellationToken);
        var replay = await RentalReplayAsync<CheckoutRentalResponse>(command.RequestId, hash, command.OperatorId, cancellationToken);
        if (replay is not null) { await transaction.CommitAsync(cancellationToken); return replay; }

        var products = new Dictionary<Guid, Product>();
        foreach (var item in command.Items.OrderBy(item => item.ProductId))
        {
            var product = await _context.Products.FromSqlInterpolated($"SELECT * FROM smart_pool.products WHERE id = {item.ProductId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm cho thuê.");
            EnsureRentalAvailability(product, item.Quantity);
            if (product.Price != item.ExpectedUnitPrice) throw new ServiceConflictException("Giá thuê đã thay đổi. Hãy lấy báo giá mới.");
            products.Add(product.Id, product);
        }

        var now = DateTime.UtcNow;
        var fees = command.Items.Sum(item => products[item.ProductId].Price * item.Quantity);
        var deposits = command.Items.Sum(item => item.Quantity) * RentalDepositPerUnit;
        var employeeExists = await _context.Employees.AnyAsync(employee => employee.UserId == command.OperatorId, cancellationToken);
        var order = new Order { Id = Guid.NewGuid(), UserId = null, EmployeeId = employeeExists ? command.OperatorId : null, CustomerName = command.CustomerName!.Trim(), CustomerPhone = command.CustomerPhone!.Trim(), TotalAmount = fees, FinalAmount = fees, DiscountAmount = 0, Status = "Renting", IsDeleted = false, CreatedAt = now, UpdatedAt = now };
        var payment = new Payment { Id = Guid.NewGuid(), OrderId = order.Id, Amount = fees, PaymentMethod = "MANUAL", Status = "COMPLETED", CashierId = employeeExists ? command.OperatorId : null, TransactionRef = $"RENTAL-{command.RequestId:N}", PaymentTime = now, CreatedAt = now };
        _context.Orders.Add(order);
        _context.Payments.Add(payment);
        var rentals = new List<Rental>();
        foreach (var item in command.Items)
        {
            var product = products[item.ProductId];
            await _context.Products.Where(current => current.Id == product.Id).ExecuteUpdateAsync(setters => setters
                .SetProperty(current => current.StockQuantity, product.StockQuantity - item.Quantity)
                .SetProperty(current => current.UpdatedAt, now), cancellationToken);
            _context.OrderDetails.Add(new OrderDetail { Id = Guid.NewGuid(), OrderId = order.Id, ItemType = "Product", ItemId = product.Id, Quantity = item.Quantity, UnitPrice = product.Price, CreatedAt = now });
            rentals.AddRange(Enumerable.Range(0, item.Quantity).Select(_ => new Rental { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductNameSnapshot = product.Name, FeeAmount = product.Price, DepositAmount = RentalDepositPerUnit, RentTime = now, Status = "Renting", UpdatedAt = now }));
            _context.InventoryLogs.Add(new InventoryLog { Id = Guid.NewGuid(), ProductId = product.Id, ChangeType = "RentalCheckout", Quantity = -item.Quantity, Note = $"Rental checkout order {order.Id}", CreatedBy = command.OperatorId, CreatedAt = now });
        }
        _context.Rentals.AddRange(rentals);
        var result = new CheckoutRentalResponse
        {
            OrderId = order.Id, RequestId = command.RequestId, OrderStatus = order.Status, TotalAmount = fees, FinalAmount = fees,
            RentalFeeTotal = fees, DepositTotal = deposits, TotalCollected = fees + deposits,
            Rentals = rentals.Select(rental => new CheckoutRentalItemResponse { Id = rental.Id, OrderId = order.Id, ProductId = rental.ProductId, ProductName = rental.ProductNameSnapshot!, RentTime = now, DepositAmount = rental.DepositAmount, FeeAmount = rental.FeeAmount, Status = "Renting", OrderStatus = order.Status }).ToList()
        };
        _context.RentalTransactions.Add(new RentalTransaction { Id = command.RequestId, OrderId = order.Id, PaymentId = payment.Id, Kind = "Collection", RequestHash = hash, FeeAmount = fees, DepositAmount = deposits, ConfirmedByUserId = command.OperatorId, ConfirmedAtUtc = now, ResultJson = System.Text.Json.JsonSerializer.Serialize(result) });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static void EnsureRentalAvailability(Product product, int quantity)
    {
        if (product.IsActive != true || product.IsDeleted != false || product.StockQuantity is null || product.StockQuantity < quantity)
            throw new ServiceConflictException("Sản phẩm cho thuê không khả dụng hoặc không đủ tồn kho.");
    }
}
