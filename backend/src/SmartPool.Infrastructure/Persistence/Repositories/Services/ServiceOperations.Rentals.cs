using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories
{

public sealed partial class ServiceOperations
{
    public async Task<PagedResponse<GetRentalsResponse>> GetRentalsAsync(GetRentalsQuery query, CancellationToken cancellationToken)
    {
        if (query.GroupByOrder) return await GetGroupedRentalsAsync(query, cancellationToken);
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
        foreach (var item in items)
        {
            item.Quantity = 1;
            item.ReturnedQuantity = item.Status == "Returned" ? 1 : 0;
            item.OutstandingQuantity = item.Status == "Renting" ? 1 : 0;
            item.NextRentalId = item.Status == "Renting" ? item.Id : null;
        }
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
        var productLock = await _context.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM smart_pool.products WHERE id = {rental.ProductId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (productLock == Guid.Empty) throw new KeyNotFoundException("Không tìm thấy sản phẩm cho thuê.");
        var rentalLock = await _context.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM smart_pool.rentals WHERE id = {command.RentalId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (rentalLock == Guid.Empty) throw new KeyNotFoundException("Không tìm thấy lượt thuê.");
        var returned = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.rentals SET status = 'Returned', return_time = now(), updated_at = now() WHERE id = {command.RentalId} AND status = 'Renting'", cancellationToken);
        if (returned == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ServiceConflictException("Lượt thuê đã được trả hoặc không thể trả.");
        }
        var stockUpdated = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.products SET stock_quantity = stock_quantity + 1, updated_at = now() WHERE id = {rental.ProductId} AND stock_quantity IS NOT NULL AND stock_quantity < {int.MaxValue}", cancellationToken);
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

    public async Task<ReturnRentalQuantityResponse> ReturnRentalQuantityAsync(
        ReturnRentalQuantityCommand command,
        CancellationToken cancellationToken)
    {
        var rentalIds = command.RentalIds
            ?? throw new InvalidOperationException("Validated quantity-return requests require rental identifiers.");
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var product = await _context.Products.AsNoTracking()
            .Where(item => item.Id == command.ProductId)
            .Select(item => new { item.Id, item.StockQuantity })
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) throw new KeyNotFoundException("Không tìm thấy sản phẩm cho thuê.");

        var lockedProduct = await _context.Database.SqlQuery<Guid>(
                $"SELECT id AS \"Value\" FROM smart_pool.products WHERE id = {command.ProductId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (lockedProduct == Guid.Empty) throw new KeyNotFoundException("Không tìm thấy sản phẩm cho thuê.");

        foreach (var rentalId in rentalIds.Order())
        {
            var lockedId = await _context.Database.SqlQuery<Guid>(
                    $"SELECT id AS \"Value\" FROM smart_pool.rentals WHERE id = {rentalId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (lockedId == Guid.Empty) throw new KeyNotFoundException("Không tìm thấy lượt thuê đã chọn.");
        }

        var rentals = await _context.Rentals.AsNoTracking()
            .Where(item => rentalIds.Contains(item.Id))
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.OrderId, item.ProductId, item.Status })
            .ToListAsync(cancellationToken);
        if (rentals.Count != rentalIds.Count)
            throw new KeyNotFoundException("Không tìm thấy lượt thuê đã chọn.");
        if (rentals.Any(item => item.OrderId != command.OrderId || item.ProductId != command.ProductId))
            throw new ServiceConflictException("Một hoặc nhiều lượt thuê không thuộc đơn hàng và sản phẩm đã chọn.");

        var returnedCount = rentals.Count(item => item.Status == "Returned");
        if (returnedCount == rentals.Count)
        {
            await transaction.CommitAsync(cancellationToken);
            return await BuildReturnQuantityResponseAsync(command, new ReturnQuantityOutcome(0, true), cancellationToken);
        }
        if (returnedCount != 0 || rentals.Any(item => item.Status != "Renting"))
            throw new ServiceConflictException("Một hoặc nhiều lượt thuê đã được trả hoặc không thể trả.");

        var now = DateTime.UtcNow;
        var changedRentals = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE smart_pool.rentals SET status = 'Returned', return_time = {now}, updated_at = {now} WHERE id = ANY ({rentalIds.ToArray()}) AND status = 'Renting'",
            cancellationToken);
        if (changedRentals != rentalIds.Count)
            throw new ServiceConflictException("Một hoặc nhiều lượt thuê đã được trả hoặc không thể trả.");

        var stockChanged = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE smart_pool.products SET stock_quantity = stock_quantity + {rentalIds.Count}, updated_at = {now} WHERE id = {command.ProductId} AND stock_quantity IS NOT NULL AND stock_quantity <= {int.MaxValue - rentalIds.Count}",
            cancellationToken);
        if (stockChanged != 1)
            throw new ServiceConflictException("Sản phẩm cho thuê không thể nhận lại tồn kho.");

        _context.InventoryLogs.Add(new InventoryLog
        {
            Id = Guid.NewGuid(), ProductId = command.ProductId, ChangeType = "RentalReturn",
            Quantity = rentalIds.Count, Note = $"Rental return order {command.OrderId}",
            CreatedBy = command.OperatorId, CreatedAt = now
        });
        await _context.SaveChangesAsync(cancellationToken);
        var response = await BuildReturnQuantityResponseAsync(
            command, new ReturnQuantityOutcome(rentalIds.Count, false), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private async Task<ReturnRentalQuantityResponse> BuildReturnQuantityResponseAsync(
        ReturnRentalQuantityCommand command,
        ReturnQuantityOutcome outcome,
        CancellationToken cancellationToken)
    {
        var totals = await _context.Rentals.AsNoTracking()
            .Where(item => item.OrderId == command.OrderId && item.ProductId == command.ProductId)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                Quantity = group.Count(),
                Returned = group.Count(item => item.Status == "Returned"),
                Outstanding = group.Count(item => item.Status == "Renting")
            })
            .SingleAsync(cancellationToken);
        var stock = await _context.Products.AsNoTracking()
            .Where(item => item.Id == command.ProductId)
            .Select(item => item.StockQuantity)
            .SingleAsync(cancellationToken);
        return new ReturnRentalQuantityResponse
        {
            OrderId = command.OrderId, ProductId = command.ProductId, ReturnedQuantity = outcome.ReturnedQuantity,
            AlreadyReturned = outcome.AlreadyReturned,
            StockQuantity = stock ?? throw new ServiceConflictException("Sản phẩm cho thuê không thể nhận lại tồn kho."),
            RentedQuantity = totals.Quantity, TotalReturnedQuantity = totals.Returned,
            OutstandingQuantity = totals.Outstanding
        };
    }

    private sealed record ReturnQuantityOutcome(int ReturnedQuantity, bool AlreadyReturned);
}
}
