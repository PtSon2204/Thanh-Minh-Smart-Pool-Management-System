using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Infrastructure.Persistence.TempModels;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed partial class ServiceOperations
{
    public async Task<ServiceOperationResult<PageDto<RentalDto>>> GetRentalsAsync(RentalListQuery query, CancellationToken cancellationToken)
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
        var items = await rentals.OrderByDescending(rental => rental.RentTime).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(rental => new RentalDto(rental.Id, rental.OrderId, rental.ProductId, rental.Product.Name, rental.RentTime, rental.ReturnTime, rental.DepositAmount, rental.Status ?? string.Empty, rental.Order.Status)).ToListAsync(cancellationToken);
        return ServiceOperationResult<PageDto<RentalDto>>.Success(new(items, total, query.Page, query.PageSize));
    }

    public async Task<ServiceOperationResult<RentalCheckoutDto>> CheckoutRentalAsync(RentalCheckoutRequest request, Guid operatorId, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var stockUpdated = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.products SET stock_quantity = stock_quantity - {request.Quantity}, updated_at = now() WHERE id = {request.ProductId} AND type = 'Rental' AND is_active = true AND is_deleted = false AND stock_quantity IS NOT NULL AND stock_quantity >= {request.Quantity}", cancellationToken);
        if (stockUpdated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var exists = await _context.Products.AsNoTracking().AnyAsync(product => product.Id == request.ProductId, cancellationToken);
            return exists ? Conflict<RentalCheckoutDto>(nameof(request.Quantity), "The rental product is unavailable or has insufficient stock.") : NotFound<RentalCheckoutDto>(nameof(request.ProductId));
        }
        var product = await _context.Products.AsNoTracking().SingleAsync(item => item.Id == request.ProductId, cancellationToken);
        var now = DateTime.UtcNow;
        var total = product.Price * request.Quantity;
        var order = new Order { Id = Guid.NewGuid(), UserId = operatorId, CustomerName = request.CustomerName?.Trim(), CustomerPhone = request.CustomerPhone?.Trim(), TotalAmount = total, DiscountAmount = 0, FinalAmount = total, Status = "Pending", IsDeleted = false, CreatedAt = now, UpdatedAt = now };
        var detail = new OrderDetail { Id = Guid.NewGuid(), OrderId = order.Id, ItemType = "Product", ItemId = product.Id, Quantity = request.Quantity, UnitPrice = product.Price, CreatedAt = now };
        var rentals = Enumerable.Range(0, request.Quantity).Select(_ => new Rental { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, RentTime = now, DepositAmount = request.DepositPerUnit, Status = "Renting", UpdatedAt = now }).ToList();
        var log = new InventoryLog { Id = Guid.NewGuid(), ProductId = product.Id, ChangeType = "RentalCheckout", Quantity = -request.Quantity, Note = $"Rental checkout order {order.Id}", CreatedBy = operatorId, CreatedAt = now };
        _context.Orders.Add(order);
        _context.OrderDetails.Add(detail);
        _context.Rentals.AddRange(rentals);
        _context.InventoryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var items = rentals.Select(rental => ToDto(rental, product.Name, order.Status)).ToList();
        return ServiceOperationResult<RentalCheckoutDto>.Success(new(order.Id, total, total, detail.Id, items));
    }

    public async Task<ServiceOperationResult<RentalReturnDto>> ReturnRentalAsync(Guid rentalId, Guid operatorId, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var rental = await _context.Rentals.AsNoTracking().Include(item => item.Product).Include(item => item.Order).SingleOrDefaultAsync(item => item.Id == rentalId, cancellationToken);
        if (rental is null) return NotFound<RentalReturnDto>(nameof(rentalId));
        var returned = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.rentals SET status = 'Returned', return_time = now(), updated_at = now() WHERE id = {rentalId} AND status = 'Renting'", cancellationToken);
        if (returned == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict<RentalReturnDto>(nameof(rentalId), "The rental was already returned or cannot be returned.");
        }
        var stockUpdated = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.products SET stock_quantity = stock_quantity + 1, updated_at = now() WHERE id = {rental.ProductId} AND stock_quantity IS NOT NULL", cancellationToken);
        if (stockUpdated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict<RentalReturnDto>(nameof(rentalId), "The rental product cannot accept a stock return.");
        }
        var stockQuantity = await _context.Products.AsNoTracking().Where(product => product.Id == rental.ProductId).Select(product => product.StockQuantity!.Value).SingleAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var log = new InventoryLog { Id = Guid.NewGuid(), ProductId = rental.ProductId, ChangeType = "RentalReturn", Quantity = 1, Note = $"Rental return {rental.Id}", CreatedBy = operatorId, CreatedAt = now };
        _context.InventoryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var returnedRental = new RentalDto(rental.Id, rental.OrderId, rental.ProductId, rental.Product.Name, rental.RentTime, now, rental.DepositAmount, "Returned", rental.Order.Status);
        return ServiceOperationResult<RentalReturnDto>.Success(new(returnedRental, stockQuantity));
    }
}
