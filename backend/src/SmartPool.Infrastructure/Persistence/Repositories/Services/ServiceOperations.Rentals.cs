using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services;

public sealed partial class ServiceOperations
{
    public async Task<PagedResponse<GetRentalsResponse>> GetRentalsAsync(GetRentalsQuery query, CancellationToken cancellationToken)
    {
        if (query.GroupByOrder) return await GetGroupedRentalsAsync(query, cancellationToken);
        var rentals = _context.Rentals.AsNoTracking().AsQueryable();
        if (query.ProductId is not null) rentals = rentals.Where(item => item.ProductId == query.ProductId);
        if (!string.IsNullOrWhiteSpace(query.Status))
            rentals = query.Status == "Completed" ? rentals.Where(item => item.Order.Status == "Completed")
                : query.Status == "Resolved" ? rentals.Where(item => item.Status == "Returned" || item.Status == "Damaged" || item.Status == "Lost") : rentals.Where(item => item.Status == query.Status);
        if (query.Date is not null)
        {
            var start = query.Date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            rentals = rentals.Where(item => item.RentTime >= start && item.RentTime < end);
        }
        var total = await rentals.CountAsync(cancellationToken);
        var items = await rentals.OrderByDescending(item => item.RentTime).ThenBy(item => item.Id)
            .Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new GetRentalsResponse
            {
                Id = item.Id, OrderId = item.OrderId, ProductId = item.ProductId, ProductName = item.ProductNameSnapshot ?? item.Product.Name,
                RentTime = item.RentTime, ReturnTime = item.ReturnTime, DepositAmount = item.DepositAmount,
                Status = item.Status ?? string.Empty, OrderStatus = item.Order.Status, CustomerName = item.Order.CustomerName, CustomerPhone = item.Order.CustomerPhone,
                Quantity = 1, ReturnedQuantity = item.Status == "Returned" ? 1 : 0, DamagedQuantity = item.Status == "Damaged" ? 1 : 0,
                LostQuantity = item.Status == "Lost" ? 1 : 0, OutstandingQuantity = item.Status == "Renting" ? 1 : 0,
                ResolvedQuantity = item.Status == "Returned" || item.Status == "Damaged" || item.Status == "Lost" ? 1 : 0,
                CollectionVerified = _context.RentalTransactions.Any(transaction => transaction.OrderId == item.OrderId && transaction.Kind == "Collection")
            }).ToListAsync(cancellationToken);
        foreach (var item in items.Where(item => item.Status == "Renting"))
        {
            item.NextRentalId = item.Id;
            item.OutstandingRentalIds = [item.Id];
        }
        return new PagedResponse<GetRentalsResponse> { Items = items, TotalCount = total, PageIndex = query.PageIndex, PageSize = query.PageSize };
    }
}
