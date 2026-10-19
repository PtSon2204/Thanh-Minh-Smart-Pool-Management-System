using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.GoodsReceipts;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed class GoodsReceiptOperations(SmartPoolDbContext context, TimeProvider timeProvider) : IGoodsReceiptOperations
{
    public async Task<GoodsReceiptResponse> CreateAsync(CreateGoodsReceiptCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({command.Id.ToString()}, 0))", cancellationToken);
        var receiptCode = command.ReceiptCode.Trim().ToUpperInvariant();
        var receivedAt = new DateTime(command.ReceivedAt.Ticks / 10 * 10, DateTimeKind.Utc);
        var existing = await Receipts().SingleOrDefaultAsync(receipt => receipt.Id == command.Id, cancellationToken);
        if (existing is not null)
        {
            // Return the original result only for an identical retry, including after posting.
            if (existing.CreatedBy != command.OperatorId || existing.ReceiptCode != receiptCode ||
                existing.ReceivedAt != receivedAt || existing.SupplierName != Clean(command.SupplierName) ||
                existing.Note != Clean(command.Note) || existing.Details.Count != command.Details.Count ||
                existing.Details.Any(line => !command.Details.Any(candidate => candidate.ProductId == line.ProductId &&
                    candidate.Quantity == line.Quantity && candidate.UnitCost == line.UnitCost)))
                throw new ServiceConflictException("Mã yêu cầu đã được dùng cho một phiếu khác. Hãy mở lại phiếu đã tạo.");
            return Map(existing);
        }

        var productIds = command.Details.Select(line => line.ProductId).ToArray();
        var products = await context.Products.AsNoTracking().Where(product => productIds.Contains(product.Id) &&
            product.IsDeleted != true && product.StockQuantity != null).ToDictionaryAsync(product => product.Id, cancellationToken);
        if (products.Count != productIds.Length)
            throw new ServiceConflictException("Chỉ được nhập sản phẩm tồn tại, chưa xóa và có theo dõi tồn kho.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var receipt = new GoodsReceipt
        {
            Id = command.Id, ReceiptCode = receiptCode, ReceivedAt = receivedAt,
            SupplierName = Clean(command.SupplierName), Note = Clean(command.Note), Status = "Draft",
            CreatedBy = command.OperatorId, CreatedAt = now, UpdatedAt = now,
            Details = command.Details.Select(line => new GoodsReceiptDetail
            {
                Id = Guid.NewGuid(), ProductId = line.ProductId, Quantity = line.Quantity, UnitCost = line.UnitCost
            }).ToList()
        };
        context.GoodsReceipts.Add(receipt);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_goods_receipts_code" })
        {
            throw new ServiceConflictException("Mã phiếu nhập đã tồn tại.");
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(receipt.Id, cancellationToken);
    }

    public async Task<PagedResponse<GoodsReceiptResponse>> GetAllAsync(GetGoodsReceiptsQuery query, CancellationToken cancellationToken)
    {
        var receipts = Receipts();
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim().ToLower();
            receipts = receipts.Where(receipt => receipt.ReceiptCode.ToLower().Contains(search) ||
                receipt.SupplierName != null && receipt.SupplierName.ToLower().Contains(search));
        }
        if (query.Status is not null) receipts = receipts.Where(receipt => receipt.Status == query.Status);
        if (query.ProductId is not null) receipts = receipts.Where(receipt => receipt.Details.Any(line => line.ProductId == query.ProductId));
        var total = await receipts.CountAsync(cancellationToken);
        var rows = await receipts.OrderByDescending(receipt => receipt.CreatedAt).ThenBy(receipt => receipt.Id)
            .Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<GoodsReceiptResponse>
        {
            Items = rows.Select(Map).ToList(), TotalCount = total, PageIndex = query.PageIndex, PageSize = query.PageSize
        };
    }

    public async Task<GoodsReceiptResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await Receipts().SingleOrDefaultAsync(receipt => receipt.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu nhập."));

    public async Task<GoodsReceiptResponse> PostAsync(Guid id, Guid operatorId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var receipt = await LockReceiptAsync(id, cancellationToken);
        // A retry is safe: Posted never adds stock a second time.
        if (receipt.Status == "Posted") return await GetByIdAsync(id, cancellationToken);
        if (receipt.Status != "Draft") throw new ServiceConflictException("Chỉ được xác nhận phiếu nháp.");
        var details = await context.GoodsReceiptDetails.AsNoTracking().Where(line => line.ReceiptId == id)
            .OrderBy(line => line.ProductId).ToListAsync(cancellationToken);
        if (details.Count == 0) throw new ServiceConflictException("Phiếu nhập không có dòng hàng.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var line in details)
        {
            // Lock products in a stable order and guard against int overflow; never update selling/rental price.
            var changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE smart_pool.products SET stock_quantity = stock_quantity + {line.Quantity}, updated_at = {now}
                WHERE id = {line.ProductId} AND is_deleted IS NOT TRUE AND stock_quantity IS NOT NULL
                    AND stock_quantity <= {int.MaxValue - line.Quantity}
                """, cancellationToken);
            if (changed != 1) throw new ServiceConflictException("Sản phẩm đã bị xóa, không theo dõi tồn hoặc số lượng vượt giới hạn. Phiếu chưa được nhập kho.");
            context.InventoryLogs.Add(new InventoryLog
            {
                Id = Guid.NewGuid(), ProductId = line.ProductId, ReceiptDetailId = line.Id,
                ChangeType = "GoodsReceipt", Quantity = line.Quantity, CreatedAt = now,
                CreatedBy = operatorId, Note = $"Nhập kho theo phiếu {receipt.ReceiptCode}"
            });
        }
        await context.GoodsReceipts.Where(item => item.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Status, "Posted").SetProperty(item => item.UpdatedAt, now), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<GoodsReceiptResponse> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var receipt = await LockReceiptAsync(id, cancellationToken);
        if (receipt.Status == "Cancelled") return await GetByIdAsync(id, cancellationToken);
        if (receipt.Status != "Draft") throw new ServiceConflictException("Phiếu đã nhập kho không được sửa hoặc hủy trực tiếp.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await context.GoodsReceipts.Where(item => item.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Status, "Cancelled").SetProperty(item => item.UpdatedAt, now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private IQueryable<GoodsReceipt> Receipts() => context.GoodsReceipts.AsNoTracking()
        .Include(receipt => receipt.Details).ThenInclude(line => line.Product);

    private async Task<GoodsReceipt> LockReceiptAsync(Guid id, CancellationToken cancellationToken) =>
        await context.GoodsReceipts.FromSqlInterpolated($"SELECT * FROM smart_pool.goods_receipts WHERE id = {id} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy phiếu nhập.");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static GoodsReceiptResponse Map(GoodsReceipt receipt) => new()
    {
        Id = receipt.Id, ReceiptCode = receipt.ReceiptCode, ReceivedAt = receipt.ReceivedAt,
        SupplierName = receipt.SupplierName, Status = receipt.Status, CreatedBy = receipt.CreatedBy,
        Note = receipt.Note, CreatedAt = receipt.CreatedAt, UpdatedAt = receipt.UpdatedAt,
        Details = receipt.Details.OrderBy(line => line.Product.Name).Select(line => new GoodsReceiptDetailResponse
        {
            Id = line.Id, ProductId = line.ProductId, ProductName = line.Product.Name,
            Quantity = line.Quantity, UnitCost = line.UnitCost
        }).ToList()
    };
}
