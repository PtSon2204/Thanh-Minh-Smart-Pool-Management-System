using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.AdjustStock;
using SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories
{

public sealed partial class ServiceOperations
{
    public async Task<AdjustStockResponse> AdjustStockAsync(AdjustStockCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var changed = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE smart_pool.products SET stock_quantity = stock_quantity + {command.Delta}, updated_at = now() WHERE id = {command.ServiceId} AND is_deleted = false AND stock_quantity IS NOT NULL AND stock_quantity + {command.Delta} >= 0", cancellationToken);
        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var exists = await _context.Products.AsNoTracking().AnyAsync(product => product.Id == command.ServiceId, cancellationToken);
            if (!exists) throw new KeyNotFoundException("Không tìm thấy dịch vụ.");
            throw new ServiceConflictException("Điều chỉnh làm tồn kho âm hoặc dịch vụ không có tồn kho.");
        }
        var stockQuantity = await _context.Products.AsNoTracking().Where(product => product.Id == command.ServiceId).Select(product => product.StockQuantity!.Value).SingleAsync(cancellationToken);
        var log = new InventoryLog { Id = Guid.NewGuid(), ProductId = command.ServiceId, ChangeType = "Adjustment", Quantity = command.Delta, Note = command.Note.Trim(), CreatedBy = command.OperatorId, CreatedAt = DateTime.UtcNow };
        _context.InventoryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AdjustStockResponse { Id = log.Id, ProductId = log.ProductId, ChangeType = log.ChangeType, Quantity = log.Quantity, Note = log.Note, CreatedBy = log.CreatedBy, CreatedAt = log.CreatedAt, StockQuantity = stockQuantity };
    }

    public async Task<PagedResponse<GetInventoryHistoryResponse>> GetInventoryHistoryAsync(GetInventoryHistoryQuery query, CancellationToken cancellationToken)
    {
        var productExists = await _context.Products.AsNoTracking().AnyAsync(product => product.Id == query.ServiceId, cancellationToken);
        if (!productExists) throw new KeyNotFoundException("Không tìm thấy dịch vụ.");
        var logs = _context.InventoryLogs.AsNoTracking().Where(log => log.ProductId == query.ServiceId);
        if (query.Date.HasValue)
        {
            var start = query.Date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            logs = logs.Where(log => log.CreatedAt >= start && log.CreatedAt < end);
        }
        var total = await logs.CountAsync(cancellationToken);
        var items = await logs.OrderByDescending(log => log.CreatedAt).Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).Select(log => new GetInventoryHistoryResponse { Id = log.Id, ProductId = log.ProductId, ChangeType = log.ChangeType, Quantity = log.Quantity, Note = log.Note, CreatedBy = log.CreatedBy, CreatedAt = log.CreatedAt }).ToListAsync(cancellationToken);
        return new PagedResponse<GetInventoryHistoryResponse> { Items = items, TotalCount = total, PageIndex = query.PageIndex, PageSize = query.PageSize };
    }
}
}
