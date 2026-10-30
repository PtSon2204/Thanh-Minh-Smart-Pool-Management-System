using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services;

public sealed partial class ServiceOperations
{
    public async Task<ReturnRentalResponse> ReturnRentalAsync(ReturnRentalCommand command, CancellationToken cancellationToken)
    {
        await new ReturnRentalValidator().ValidateAndThrowAsync(command, cancellationToken);
        var rental = await _context.Rentals.AsNoTracking().SingleOrDefaultAsync(item => item.Id == command.RentalId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lượt thuê.");
        var request = new ReturnRentalQuantityCommand
        {
            OrderId = rental.OrderId, ProductId = rental.ProductId, OperatorId = command.OperatorId,
            RequestId = command.RequestId, SettlementConfirmed = command.SettlementConfirmed, Reason = command.Reason, RentalIds = []
        };
        switch (command.Condition)
        {
            case "Normal": request.RentalIds.Add(rental.Id); break;
            case "Damaged": request.DamagedRentalIds.Add(rental.Id); break;
            case "Lost": request.LostRentalIds.Add(rental.Id); break;
            default: throw new InvalidOperationException("Validated condition was not recognized.");
        }
        var result = await ReturnRentalQuantityAsync(request, cancellationToken);
        var saved = await _context.Rentals.AsNoTracking().Include(item => item.Product).SingleAsync(item => item.Id == rental.Id, cancellationToken);
        return new ReturnRentalResponse
        {
            StockQuantity = result.StockQuantity,
            Rental = new ReturnRentalItemResponse { Id = saved.Id, OrderId = saved.OrderId, ProductId = saved.ProductId, ProductName = saved.ProductNameSnapshot ?? saved.Product.Name, RentTime = saved.RentTime, ReturnTime = saved.ReturnTime, DepositAmount = saved.DepositAmount, Status = saved.Status ?? string.Empty, OrderStatus = result.OrderStatus }
        };
    }

    public async Task<ReturnRentalQuantityResponse> ReturnRentalQuantityAsync(ReturnRentalQuantityCommand command, CancellationToken cancellationToken)
    {
        await new ReturnRentalQuantityValidator().ValidateAndThrowAsync(command, cancellationToken);
        var normalIds = command.RentalIds!;
        var ids = command.SelectedIds;
        var hash = RentalRequestHash(new { Operation = "Settlement", command.OperatorId, command.OrderId, command.ProductId, command.SettlementConfirmed,
            Normal = normalIds.Order().ToArray(), Damaged = command.DamagedRentalIds.Order().ToArray(), Lost = command.LostRentalIds.Order().ToArray(), Reason = command.Reason?.Trim() });
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await LockRentalRequestAsync(command.RequestId, cancellationToken);
        var replay = await RentalReplayAsync<ReturnRentalQuantityResponse>(command.RequestId, hash, command.OperatorId, cancellationToken);
        if (replay is not null) { await transaction.CommitAsync(cancellationToken); return replay; }

        var order = await _context.Orders.FromSqlInterpolated($"SELECT * FROM smart_pool.orders WHERE id = {command.OrderId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ServiceConflictException("Lượt thuê không thuộc đơn và sản phẩm đã chọn.");
        if (order.IsDeleted != false) throw new ServiceConflictException("Đơn thuê không khả dụng.");
        if (!await _context.RentalTransactions.AnyAsync(item => item.OrderId == order.Id && item.Kind == "Collection", cancellationToken))
            throw new ServiceConflictException("Đơn thuê cũ chưa xác minh thu tiền. Chỉ được xem lịch sử, không tự hoàn cọc.");
        var product = await _context.Products.FromSqlInterpolated($"SELECT * FROM smart_pool.products WHERE id = {command.ProductId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm cho thuê.");
        foreach (var id in ids.Order())
        {
            var locked = await _context.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM smart_pool.rentals WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (locked == Guid.Empty) throw new KeyNotFoundException("Không tìm thấy lượt thuê đã chọn.");
        }
        var rentals = await _context.Rentals.AsNoTracking().Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
        if (rentals.Any(item => item.OrderId != order.Id || item.ProductId != product.Id))
            throw new ServiceConflictException("Lượt thuê không thuộc đơn và sản phẩm đã chọn.");
        if (rentals.Any(item => item.Status != "Renting" || item.SettlementId is not null))
            throw new ServiceConflictException("Một hoặc nhiều lượt thuê đã được xử lý. Hãy làm mới chi tiết.");
        if (rentals.Any(item => item.DepositAmount is null || item.FeeAmount is null))
            throw new ServiceConflictException("Không có đủ thông tin cọc đã xác minh để quyết toán.");
        var refund = rentals.Where(item => normalIds.Contains(item.Id)).Sum(item => item.DepositAmount!.Value);
        var deposit = rentals.Sum(item => item.DepositAmount!.Value);
        var now = _clock.GetUtcNow().UtcDateTime;
        var settlement = new RentalTransaction { Id = command.RequestId, OrderId = order.Id, ProductId = product.Id, Kind = "Settlement", RequestHash = hash,
            DepositAmount = deposit, RefundAmount = refund, RetainedAmount = deposit - refund, RefundMethod = refund > 0 ? "CASH" : null,
            ConfirmedByUserId = command.OperatorId, ConfirmedAtUtc = now, Reason = command.Reason?.Trim() };
        _context.RentalTransactions.Add(settlement);
        await _context.SaveChangesAsync(cancellationToken);
        foreach (var rental in rentals)
        {
            var status = normalIds.Contains(rental.Id) ? "Returned" : command.DamagedRentalIds.Contains(rental.Id) ? "Damaged" : "Lost";
            DateTime? returnedAt = status == "Lost" ? null : now;
            await _context.Rentals.Where(item => item.Id == rental.Id).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, status).SetProperty(item => item.ReturnTime, returnedAt)
                .SetProperty(item => item.ResolvedAtUtc, now).SetProperty(item => item.SettlementId, settlement.Id)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        }
        if (product.StockQuantity is null || product.StockQuantity > int.MaxValue - normalIds.Count)
            throw new ServiceConflictException("Không thể cập nhật số lượng có thể cho thuê.");
        var stock = product.StockQuantity.Value + normalIds.Count;
        if (normalIds.Count > 0)
        {
            await _context.Products.Where(item => item.Id == product.Id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.StockQuantity, stock).SetProperty(item => item.UpdatedAt, now), cancellationToken);
            _context.InventoryLogs.Add(new InventoryLog { Id = Guid.NewGuid(), ProductId = product.Id, ChangeType = "RentalReturn", Quantity = normalIds.Count, Note = $"Rental settlement {settlement.Id}", CreatedBy = command.OperatorId, CreatedAt = now });
        }
        var stillRenting = await _context.Rentals.AnyAsync(item => item.OrderId == order.Id && item.Status == "Renting", cancellationToken);
        var orderStatus = stillRenting ? "Renting" : "Completed";
        await _context.Orders.Where(item => item.Id == order.Id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, orderStatus).SetProperty(item => item.UpdatedAt, now), cancellationToken);
        var group = await _context.Rentals.AsNoTracking().Where(item => item.OrderId == order.Id && item.ProductId == product.Id).ToListAsync(cancellationToken);
        var result = new ReturnRentalQuantityResponse
        {
            OrderId = order.Id, ProductId = product.Id, RequestId = command.RequestId, StockQuantity = stock,
            ReturnedQuantity = normalIds.Count, NormalQuantity = normalIds.Count, DamagedQuantity = command.DamagedRentalIds.Count, LostQuantity = command.LostRentalIds.Count,
            RentedQuantity = group.Count, TotalReturnedQuantity = group.Count(item => item.Status == "Returned"), OutstandingQuantity = group.Count(item => item.Status == "Renting"),
            RefundAmount = refund, RetainedAmount = deposit - refund, DepositAmount = deposit, OrderStatus = orderStatus
        };
        settlement.ResultJson = JsonSerializer.Serialize(result);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
