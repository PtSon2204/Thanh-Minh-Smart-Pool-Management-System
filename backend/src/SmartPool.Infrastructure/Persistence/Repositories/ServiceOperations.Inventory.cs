using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Infrastructure.Persistence.TempModels;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed partial class ServiceOperations
{
    public async Task<ServiceOperationResult<StockAdjustmentDto>> AdjustStockAsync(Guid serviceId, AdjustStockRequest request, Guid operatorId, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var changed = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.products SET stock_quantity = stock_quantity + {request.Delta}, updated_at = now() WHERE id = {serviceId} AND is_deleted = false AND stock_quantity IS NOT NULL AND stock_quantity + {request.Delta} >= 0", cancellationToken);
        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var exists = await _context.Products.AsNoTracking().AnyAsync(product => product.Id == serviceId, cancellationToken);
            return exists ? Conflict<StockAdjustmentDto>(nameof(request.Delta), "The adjustment would make stock negative or stock is unavailable.") : NotFound<StockAdjustmentDto>(nameof(serviceId));
        }
        var stockQuantity = await _context.Products.AsNoTracking().Where(product => product.Id == serviceId).Select(product => product.StockQuantity!.Value).SingleAsync(cancellationToken);
        var log = new InventoryLog { Id = Guid.NewGuid(), ProductId = serviceId, ChangeType = "Adjustment", Quantity = request.Delta, Note = request.Note.Trim(), CreatedBy = operatorId, CreatedAt = DateTime.UtcNow };
        _context.InventoryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceOperationResult<StockAdjustmentDto>.Success(new(ToDto(log), stockQuantity));
    }

    public async Task<ServiceOperationResult<PageDto<InventoryLogDto>>> GetInventoryHistoryAsync(Guid serviceId, InventoryHistoryQuery query, CancellationToken cancellationToken)
    {
        var productExists = await _context.Products.AsNoTracking().AnyAsync(product => product.Id == serviceId, cancellationToken);
        if (!productExists) return NotFound<PageDto<InventoryLogDto>>(nameof(serviceId));
        var logs = _context.InventoryLogs.AsNoTracking().Where(log => log.ProductId == serviceId);
        if (query.Date.HasValue)
        {
            var start = query.Date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            logs = logs.Where(log => log.CreatedAt >= start && log.CreatedAt < end);
        }
        var total = await logs.CountAsync(cancellationToken);
        var items = await logs.OrderByDescending(log => log.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(log => new InventoryLogDto(log.Id, log.ProductId, log.ChangeType, log.Quantity, log.Note, log.CreatedBy, log.CreatedAt)).ToListAsync(cancellationToken);
        return ServiceOperationResult<PageDto<InventoryLogDto>>.Success(new(items, total, query.Page, query.PageSize));
    }
}
