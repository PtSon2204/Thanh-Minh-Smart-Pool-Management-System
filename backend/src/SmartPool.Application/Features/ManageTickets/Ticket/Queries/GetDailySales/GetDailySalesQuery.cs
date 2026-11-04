using MediatR;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetDailySales;

public sealed class GetDailySalesQuery : IRequest<DailySalesResponse>
{
    public DateOnly Date { get; init; }
}

public sealed class DailySalesResponse
{
    public DateOnly Date { get; init; }
    public int TotalTickets { get; init; }
    public int TotalOrders { get; init; }
    public decimal AverageTicketsPerOrder { get; init; }
    public int? PeakHour { get; init; }
    public int PeakHourTickets { get; init; }
    public int AdmissionCount { get; init; }
    public string AdmissionLabel { get; init; } = "Lượt vào thành công";
    public IReadOnlyList<DailySalesHour> HourlySales { get; init; } = [];
    public IReadOnlyList<DailySalesGroup> TicketTypes { get; init; } = [];
    public IReadOnlyList<DailySalesGroup> PaymentMethods { get; init; } = [];
}

public sealed class DailySalesHour
{
    public int Hour { get; init; }
    public int TicketCount { get; init; }
}

public sealed class DailySalesGroup
{
    public string Name { get; init; } = string.Empty;
    public int TicketCount { get; init; }
}
