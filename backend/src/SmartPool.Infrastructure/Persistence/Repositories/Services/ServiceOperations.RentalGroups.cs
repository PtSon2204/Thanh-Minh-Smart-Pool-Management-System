using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;
using SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

namespace SmartPool.Infrastructure.Persistence.Repositories;

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
            ProductName = rental.Product.Name,
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
            Status = group.Count(rental => rental.Status == "Renting") > 0 ? "Renting" : "Returned"
        });

        if (!string.IsNullOrWhiteSpace(query.Status))
            groups = groups.Where(group => group.Status == query.Status);
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
                rental.Status,
                ProductName = rental.Product.Name,
                rental.Order.CustomerName,
                rental.Order.CustomerPhone,
                OrderStatus = rental.Order.Status
            })
            .ToListAsync(cancellationToken);

        if (rentals.Count == 0) throw new KeyNotFoundException("Không tìm thấy lượt thuê theo đơn hàng và sản phẩm.");

        var outstanding = rentals.Where(rental => rental.Status == "Renting").Select(rental => rental.Id).ToList();
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
            OutstandingRentalIds = outstanding,
            Rentals = rentals.Select(rental => new RentalRecordDetails
            {
                Id = rental.Id,
                RentTime = rental.RentTime,
                ReturnTime = rental.ReturnTime,
                DepositAmount = rental.DepositAmount,
                Status = rental.Status ?? string.Empty
            }).ToList()
        };
    }
}
