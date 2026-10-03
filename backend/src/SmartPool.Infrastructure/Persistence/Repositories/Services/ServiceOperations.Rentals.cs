using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories
{

public sealed partial class ServiceOperations
{
    public async Task<PagedResponse<GetRentalsResponse>> GetRentalsAsync(GetRentalsQuery query, CancellationToken cancellationToken)
    {
        var rentals = _context.Rentals.AsNoTracking().Include(rental => rental.Product).Include(rental => rental.Order).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Status)) rentals = rentals.Where(rental => rental.Status == query.Status);
        if (query.ProductId.HasValue) rentals = rentals.Where(rental => rental.ProductId == query.ProductId.Value);
        if (query.Date.HasValue)
        {
            var start = query.Date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            rentals = rentals.Where(rental => rental.RentTime >= start && rental.RentTime < end);
        }
        var total = await rentals.CountAsync(cancellationToken);
        var items = await rentals.OrderByDescending(rental => rental.RentTime).Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).Select(rental => new GetRentalsResponse { Id = rental.Id, OrderId = rental.OrderId, ProductId = rental.ProductId, ProductName = rental.Product.Name, RentTime = rental.RentTime, ReturnTime = rental.ReturnTime, DepositAmount = rental.DepositAmount, Status = rental.Status ?? string.Empty, OrderStatus = rental.Order.Status }).ToListAsync(cancellationToken);
        return new PagedResponse<GetRentalsResponse> { Items = items, TotalCount = total, PageIndex = query.PageIndex, PageSize = query.PageSize };
    }

    public async Task<CheckoutRentalResponse> CheckoutRentalAsync(CheckoutRentalCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var stockUpdated = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.products SET stock_quantity = stock_quantity - {command.Quantity}, updated_at = now() WHERE id = {command.ProductId} AND type = 'Rental' AND is_active = true AND is_deleted = false AND stock_quantity IS NOT NULL AND stock_quantity >= {command.Quantity}", cancellationToken);
        if (stockUpdated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var exists = await _context.Products.AsNoTracking().AnyAsync(product => product.Id == command.ProductId, cancellationToken);
            if (!exists) throw new KeyNotFoundException("Không tìm thấy sản phẩm cho thuê.");
            throw new ServiceConflictException("Sản phẩm cho thuê không khả dụng hoặc không đủ tồn kho.");
        }
        var product = await _context.Products.AsNoTracking().SingleAsync(item => item.Id == command.ProductId, cancellationToken);
        var now = DateTime.UtcNow;
        var total = product.Price * command.Quantity;
        var order = new Order { Id = Guid.NewGuid(), UserId = command.OperatorId, CustomerName = command.CustomerName?.Trim(), CustomerPhone = command.CustomerPhone?.Trim(), TotalAmount = total, DiscountAmount = 0, FinalAmount = total, Status = "Pending", IsDeleted = false, CreatedAt = now, UpdatedAt = now };
        var detail = new OrderDetail { Id = Guid.NewGuid(), OrderId = order.Id, ItemType = "Product", ItemId = product.Id, Quantity = command.Quantity, UnitPrice = product.Price, CreatedAt = now };
        var rentals = Enumerable.Range(0, command.Quantity).Select(_ => new Rental { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, RentTime = now, DepositAmount = command.DepositPerUnit, Status = "Renting", UpdatedAt = now }).ToList();
        var log = new InventoryLog { Id = Guid.NewGuid(), ProductId = product.Id, ChangeType = "RentalCheckout", Quantity = -command.Quantity, Note = $"Rental checkout order {order.Id}", CreatedBy = command.OperatorId, CreatedAt = now };
        _context.Orders.Add(order);
        _context.OrderDetails.Add(detail);
        _context.Rentals.AddRange(rentals);
        _context.InventoryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CheckoutRentalResponse { OrderId = order.Id, TotalAmount = total, FinalAmount = total, OrderDetailId = detail.Id, Rentals = rentals.Select(rental => new CheckoutRentalItemResponse { Id = rental.Id, OrderId = rental.OrderId, ProductId = rental.ProductId, ProductName = product.Name, RentTime = rental.RentTime, ReturnTime = rental.ReturnTime, DepositAmount = rental.DepositAmount, Status = rental.Status ?? string.Empty, OrderStatus = order.Status }).ToList() };
    }

    public async Task<ReturnRentalResponse> ReturnRentalAsync(ReturnRentalCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var rental = await _context.Rentals.AsNoTracking().Include(item => item.Product).Include(item => item.Order).SingleOrDefaultAsync(item => item.Id == command.RentalId, cancellationToken);
        if (rental is null) throw new KeyNotFoundException("Không tìm thấy lượt thuê.");
        var returned = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.rentals SET status = 'Returned', return_time = now(), updated_at = now() WHERE id = {command.RentalId} AND status = 'Renting'", cancellationToken);
        if (returned == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ServiceConflictException("Lượt thuê đã được trả hoặc không thể trả.");
        }
        var stockUpdated = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.products SET stock_quantity = stock_quantity + 1, updated_at = now() WHERE id = {rental.ProductId} AND stock_quantity IS NOT NULL", cancellationToken);
        if (stockUpdated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ServiceConflictException("Sản phẩm cho thuê không thể nhận lại tồn kho.");
        }
        var stockQuantity = await _context.Products.AsNoTracking().Where(product => product.Id == rental.ProductId).Select(product => product.StockQuantity!.Value).SingleAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var log = new InventoryLog { Id = Guid.NewGuid(), ProductId = rental.ProductId, ChangeType = "RentalReturn", Quantity = 1, Note = $"Rental return {rental.Id}", CreatedBy = command.OperatorId, CreatedAt = now };
        _context.InventoryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ReturnRentalResponse { Rental = new ReturnRentalItemResponse { Id = rental.Id, OrderId = rental.OrderId, ProductId = rental.ProductId, ProductName = rental.Product.Name, RentTime = rental.RentTime, ReturnTime = now, DepositAmount = rental.DepositAmount, Status = "Returned", OrderStatus = rental.Order.Status }, StockQuantity = stockQuantity };
    }
}
}
