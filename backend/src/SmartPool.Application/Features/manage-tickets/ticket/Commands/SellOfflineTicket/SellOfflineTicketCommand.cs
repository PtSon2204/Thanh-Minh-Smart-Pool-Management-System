using MediatR;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket
{
    public class SellOfflineTicketCommand : IRequest<SellOfflineTicketResponse>
    {
        public List<SellOfflineTicketItem> Items { get; set; } = new List<SellOfflineTicketItem>();
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public DateTime? StartDate { get; set; }
        public string? VoucherCode { get; set; }
    }
}
