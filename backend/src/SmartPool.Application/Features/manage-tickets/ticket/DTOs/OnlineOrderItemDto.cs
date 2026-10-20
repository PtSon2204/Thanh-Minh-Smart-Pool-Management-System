namespace SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

public sealed class OnlineOrderItemDto
{
    public Guid TicketTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}
