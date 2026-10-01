using MediatR;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder
{
    public class ConfirmPendingOrderCommand : IRequest<ConfirmPendingOrderResponse>
    {
        public string TransactionRef { get; set; } = string.Empty;
        public decimal ActualAmount { get; set; }
    }
}
