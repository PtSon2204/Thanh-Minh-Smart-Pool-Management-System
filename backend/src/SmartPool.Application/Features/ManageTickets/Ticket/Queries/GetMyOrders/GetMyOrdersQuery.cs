using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyOrders;

public sealed class GetMyOrdersQuery : IRequest<MyOrdersResponse>
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    [Range(1, 1_000_000)]
    public int PageIndex { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;

    [RegularExpression("(?i)^(ALL|WAITING|COMPLETED|CANCELLED)?$")]
    public string? Status { get; set; }
}

public sealed class GetMyOrdersSummaryQuery : IRequest<MyOrderSummaryResponse>
{
    [JsonIgnore]
    public Guid UserId { get; set; }
}
