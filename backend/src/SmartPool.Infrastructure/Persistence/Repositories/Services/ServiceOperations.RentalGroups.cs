using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;
using SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services;

public sealed partial class ServiceOperations
{
    private async Task<PagedResponse<GetRentalsResponse>> GetGroupedRentalsAsync(
        GetRentalsQuery query, CancellationToken cancellationToken)
    {
        var rentals = _context.Rentals.AsNoTracking().AsQueryable();
        if (query.ProductId.HasValue)
            rentals = rentals.Where(rental => rental.ProductId == query.ProductId.Value);

        // Aggregate complete orders before applying status, date, or pagination.
        var groups = rentals.GroupBy(rental => new
        {
            rental.OrderId,
            rental.ProductId,
            ProductName = rental.ProductNameSnapshot ?? rental.Product.Name,
            rental.Order.CustomerName,
            rental.Order.CustomerPhone,
            OrderStatus = rental.Order.Status
        }).Select(group => new GetRentalsResponse
        {
            Id = group.Key.OrderId,
            OrderId = group.Key.OrderId,
            ProductId = group.Key.ProductId,
            ProductName = group.Key.ProductName,
            CustomerName = group.Key.CustomerName,
            CustomerPhone = group.Key.CustomerPhone,
            OrderStatus = group.Key.OrderStatus,
            RentTime = group.Min(rental => rental.RentTime),
            ReturnTime = group.Max(rental => rental.ReturnTime),
            DepositAmount = group.Sum(rental => rental.DepositAmount),
            Quantity = group.Count(),
            ReturnedQuantity = group.Count(rental => rental.Status == "Returned"),
            OutstandingQuantity = group.Count(rental => rental.Status == "Renting"),
            DamagedQuantity = group.Count(rental => rental.Status == "Damaged"),
            LostQuantity = group.Count(rental => rental.Status == "Lost"),
            ResolvedQuantity = group.Count(rental => rental.Status == "Returned" || rental.Status == "Damaged" || rental.Status == "Lost"),
            Status = group.Count(rental => rental.Status == "Renting") > 0 ? "Renting" : group.Count(rental => rental.Status == "Returned") == group.Count() ? "Returned" : "Resolved"
        });

        if (!string.IsNullOrWhiteSpace(query.Status))
            groups = query.Status == "Completed" ? groups.Where(group => group.OrderStatus == "Completed")
                : query.Status == "Resolved" ? groups.Where(group => group.OutstandingQuantity == 0)
                : query.Status == "Damaged" ? groups.Where(group => group.DamagedQuantity > 0)
                : query.Status == "Lost" ? groups.Where(group => group.LostQuantity > 0)
                : groups.Where(group => group.Status == query.Status);
        if (query.Date.HasValue)
        {
            var start = query.Date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            groups = groups.Where(group => group.RentTime >= start && group.RentTime < end);
        }

        var total = await groups.CountAsync(cancellationToken);
        var items = await groups.OrderByDescending(group => group.RentTime)
            .ThenBy(group => group.OrderId).ThenBy(group => group.ProductId)
            .Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var allOrderIds = items.Select(item => item.OrderId).Distinct().ToList();
        var verifiedOrders = await _context.RentalTransactions.AsNoTracking().Where(item => allOrderIds.Contains(item.OrderId) && item.Kind == "Collection").Select(item => item.OrderId).ToListAsync(cancellationToken);
        foreach (var item in items) item.CollectionVerified = verifiedOrders.Contains(item.OrderId);

        var orderIds = items.Where(item => item.OutstandingQuantity > 0)
            .Select(item => item.OrderId).Distinct().ToList();
        if (orderIds.Count > 0)
        {
            var availableReturns = await _context.Rentals.AsNoTracking()
                .Where(rental => orderIds.Contains(rental.OrderId) && rental.Status == "Renting")
                .Select(rental => new { rental.Id, rental.OrderId, rental.ProductId })
                .ToListAsync(cancellationToken);
            foreach (var item in items)
            {
                item.OutstandingRentalIds = availableReturns
                    .Where(rental => rental.OrderId == item.OrderId && rental.ProductId == item.ProductId)
                    .Select(rental => rental.Id).Order().ToList();
                item.NextRentalId = item.OutstandingRentalIds.Select(id => (Guid?)id).FirstOrDefault();
            }
        }

        return new PagedResponse<GetRentalsResponse>
        {
            Items = items, TotalCount = total,
            PageIndex = query.PageIndex, PageSize = query.PageSize
        };
    }

    public async Task<GetRentalDetailsResponse> GetRentalDetailsAsync(
        GetRentalDetailsQuery query, CancellationToken cancellationToken)
    {
        var rentals = await _context.Rentals.AsNoTracking()
            .Where(rental => rental.OrderId == query.OrderId && rental.ProductId == query.ProductId)
            .OrderBy(rental => rental.Id)
            .Select(rental => new
            {
                rental.Id,
                rental.RentTime,
                rental.ReturnTime,
                rental.DepositAmount,
                rental.FeeAmount,
                rental.SettlementId,
                rental.ResolvedAtUtc,
                rental.Status,
                ProductName = rental.ProductNameSnapshot ?? rental.Product.Name,
                rental.Order.CustomerName,
                rental.Order.CustomerPhone,
                OrderStatus = rental.Order.Status
            })
            .ToListAsync(cancellationToken);

        if (rentals.Count == 0) throw new KeyNotFoundException("Không tìm thấy lượt thuê theo đơn hàng và sản phẩm.");

        var outstanding = rentals.Where(rental => rental.Status == "Renting").Select(rental => rental.Id).ToList();
        var transactions = await _context.RentalTransactions.AsNoTracking()
            .Where(item => item.OrderId == query.OrderId && (item.Kind == "Collection" || item.ProductId == query.ProductId))
            .OrderBy(item => item.ConfirmedAtUtc).ThenBy(item => item.Id)
            .Select(item => new RentalTransactionDetails { Id = item.Id, Kind = item.Kind, ProductId = item.ProductId, FeeAmount = item.FeeAmount, DepositAmount = item.DepositAmount, RefundAmount = item.RefundAmount, RetainedAmount = item.RetainedAmount, RefundMethod = item.RefundMethod,
                CollectionMethod = item.Kind == "Collection" ? _context.Payments.Where(payment => payment.Id == item.PaymentId).Select(payment => payment.PaymentMethod).FirstOrDefault() : null,
                ConfirmedByUserId = item.ConfirmedByUserId, ConfirmedAtUtc = item.ConfirmedAtUtc, Reason = item.Reason })
            .ToListAsync(cancellationToken);
        var collection = transactions.SingleOrDefault(item => item.Kind == "Collection");
        return new GetRentalDetailsResponse
        {
            OrderId = query.OrderId,
            ProductId = query.ProductId,
            ProductName = rentals[0].ProductName,
            CustomerName = rentals[0].CustomerName,
            CustomerPhone = rentals[0].CustomerPhone,
            OrderStatus = rentals[0].OrderStatus,
            RentTime = rentals.Min(rental => rental.RentTime),
            ReturnTime = rentals.Max(rental => rental.ReturnTime),
            RentedQuantity = rentals.Count,
            ReturnedQuantity = rentals.Count(rental => rental.Status == "Returned"),
            OutstandingQuantity = outstanding.Count,
            TotalDepositAmount = rentals.Sum(rental => rental.DepositAmount ?? 0),
            CollectionVerified = collection is not null,
            RentalFeeTotal = rentals.Sum(rental => rental.FeeAmount ?? 0),
            RefundedDepositAmount = rentals.Where(rental => rental.Status == "Returned" && rental.SettlementId is not null).Sum(rental => rental.DepositAmount ?? 0),
            RetainedDepositAmount = rentals.Where(rental => (rental.Status == "Damaged" || rental.Status == "Lost") && rental.SettlementId is not null).Sum(rental => rental.DepositAmount ?? 0),
            OutstandingDepositAmount = rentals.Where(rental => rental.Status == "Renting").Sum(rental => rental.DepositAmount ?? 0),
            OrderCollectedAmount = collection is null ? 0 : collection.FeeAmount + collection.DepositAmount,
            DamagedQuantity = rentals.Count(rental => rental.Status == "Damaged"),
            LostQuantity = rentals.Count(rental => rental.Status == "Lost"),
            ResolvedQuantity = rentals.Count(rental => rental.Status is "Returned" or "Damaged" or "Lost"),
            Transactions = transactions,
            OutstandingRentalIds = outstanding,
            Rentals = rentals.Select(rental => new RentalRecordDetails
            {
                Id = rental.Id,
                RentTime = rental.RentTime,
                ReturnTime = rental.ReturnTime,
                DepositAmount = rental.DepositAmount,
                FeeAmount = rental.FeeAmount,
                SettlementId = rental.SettlementId,
                ResolvedAtUtc = rental.ResolvedAtUtc,
                Status = rental.Status ?? string.Empty
            }).ToList()
        };
    }
}
