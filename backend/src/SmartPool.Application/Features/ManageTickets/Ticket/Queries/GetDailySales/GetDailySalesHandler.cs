using MediatR;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetDailySales;

public sealed class GetDailySalesHandler : IRequestHandler<GetDailySalesQuery, DailySalesResponse>
{
    private const string TicketItemType = "TICKET";
    private const string CombinedPaymentLabel = "Thanh toán kết hợp";
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private readonly ITicketOrderOperations _ticketOrderOperations;
    private readonly ITicketTypeOperations _ticketTypeOperations;

    public GetDailySalesHandler(
        ITicketOrderOperations ticketOrderOperations,
        ITicketTypeOperations ticketTypeOperations)
    {
        _ticketOrderOperations = ticketOrderOperations;
        _ticketTypeOperations = ticketTypeOperations;
    }

    public async Task<DailySalesResponse> Handle(GetDailySalesQuery request, CancellationToken cancellationToken)
    {
        var (startUtc, endUtc) = VietnamTimeBoundary.GetUtcDayBounds(request.Date);
        var dailyPayments = await _ticketOrderOperations.GetDailySalesPaymentsAsync(startUtc, endUtc, cancellationToken);
        var candidateOrderIds = dailyPayments.Select(payment => payment.OrderId).Distinct().ToList();
        var completedOrders = candidateOrderIds.Count == 0
            ? new List<Order>()
            : await _ticketOrderOperations.GetOrdersByIdsAsync(candidateOrderIds, cancellationToken);
        List<Payment> completedPayments = candidateOrderIds.Count == 0
            ? new List<Payment>()
            : await _ticketOrderOperations.GetPaymentsByOrderIdsAsync(candidateOrderIds, cancellationToken);
        var paymentsByOrder = completedPayments.GroupBy(payment => payment.OrderId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var saleOrders = completedOrders
            .Where(order => paymentsByOrder.TryGetValue(order.Id, out var payments)
                && payments.Count > 0
                && payments.Sum(payment => payment.Amount) >= order.FinalAmount)
            .Select(order =>
            {
                var payments = paymentsByOrder[order.Id];
                // Checkout creates the original payment first. Confirmation stamps its
                // PaymentTime once; later receipts must not move the sale to another day.
                var originalPayment = payments.OrderBy(payment => payment.CreatedAt ?? payment.PaymentTime)
                    .ThenBy(payment => payment.Id).First();
                return new
                {
                    Order = order,
                    Payments = payments,
                    SettlementTimeUtc = originalPayment.PaymentTime
                };
            })
            .Where(sale => sale.SettlementTimeUtc.HasValue
                && sale.SettlementTimeUtc >= startUtc && sale.SettlementTimeUtc < endUtc)
            .ToList();

        var saleOrderIds = saleOrders.Select(sale => sale.Order.Id).ToList();
        List<OrderDetail> ticketDetails = saleOrderIds.Count == 0
            ? new List<OrderDetail>()
            : await _ticketOrderOperations.GetTicketOrderDetailsByOrderIdsAsync(saleOrderIds, cancellationToken);
        var ticketOrderIds = ticketDetails.Select(detail => detail.OrderId).ToHashSet();
        saleOrders = saleOrders.Where(sale => ticketOrderIds.Contains(sale.Order.Id)).ToList();
        var countedOrderIds = saleOrders.Select(sale => sale.Order.Id).ToHashSet();
        ticketDetails = ticketDetails.Where(detail => countedOrderIds.Contains(detail.OrderId)).ToList();

        var typeIds = ticketDetails.Select(detail => detail.ItemId).Distinct().ToList();
        Dictionary<Guid, TicketTypeEntity> ticketTypes = typeIds.Count == 0
            ? new Dictionary<Guid, TicketTypeEntity>()
            : (await _ticketTypeOperations.GetByIdsAsync(typeIds, cancellationToken))
                .ToDictionary(type => type.Id);

        var hourlyCounts = Enumerable.Range(0, 24).ToDictionary(hour => hour, _ => 0);
        var paymentMethodTickets = new Dictionary<string, int>(StringComparer.Ordinal);
        var saleByOrderId = saleOrders.ToDictionary(sale => sale.Order.Id);
        foreach (var detail in ticketDetails)
        {
            var count = detail.Quantity;
            var sale = saleByOrderId[detail.OrderId];
            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(sale.SettlementTimeUtc!.Value, DateTimeKind.Utc), VietnamTimeZone);
            hourlyCounts[vietnamTime.Hour] += count;

            var methodNames = sale.Payments.Where(payment => payment.PaymentTime <= sale.SettlementTimeUtc)
                .Select(payment => NormalizePaymentMethod(payment.PaymentMethod))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var paymentName = methodNames.Count > 1 ? CombinedPaymentLabel : methodNames[0];
            paymentMethodTickets[paymentName] = paymentMethodTickets.GetValueOrDefault(paymentName) + count;
        }

        var totalTickets = ticketDetails.Sum(detail => detail.Quantity);
        KeyValuePair<int, int>? peak = totalTickets == 0
            ? null
            : hourlyCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).First();
        var allowedEntries = await _ticketOrderOperations.GetAllowedEntryLogsAsync(startUtc, endUtc, cancellationToken);

        var ticketsByType = ticketDetails
            .GroupBy(detail => ticketTypes.TryGetValue(detail.ItemId, out var type) ? type.Name : "Khác")
            .Select(group => new DailySalesGroup { Name = group.Key, TicketCount = group.Sum(detail => detail.Quantity) })
            .OrderByDescending(group => group.TicketCount)
            .ThenBy(group => group.Name, StringComparer.Ordinal)
            .ToList();

        return new DailySalesResponse
        {
            Date = request.Date,
            TotalTickets = totalTickets,
            TotalOrders = saleOrders.Count,
            AverageTicketsPerOrder = saleOrders.Count == 0 ? 0 : Math.Round((decimal)totalTickets / saleOrders.Count, 2),
            PeakHour = peak?.Key,
            PeakHourTickets = peak?.Value ?? 0,
            AdmissionCount = allowedEntries.Count,
            HourlySales = hourlyCounts.Select(pair => new DailySalesHour { Hour = pair.Key, TicketCount = pair.Value }).ToList(),
            TicketTypes = ticketsByType,
            PaymentMethods = paymentMethodTickets
                .Select(pair => new DailySalesGroup { Name = pair.Key, TicketCount = pair.Value })
                .OrderByDescending(group => group.TicketCount)
                .ThenBy(group => group.Name, StringComparer.Ordinal)
                .ToList()
        };
    }

    private static string NormalizePaymentMethod(string? method) => method?.Trim().ToUpperInvariant() switch
    {
        "CASH" => "Tiền mặt",
        "TRANSFER" => "Chuyển khoản / QR",
        _ => "Khác / Chưa xác định"
    };
}
