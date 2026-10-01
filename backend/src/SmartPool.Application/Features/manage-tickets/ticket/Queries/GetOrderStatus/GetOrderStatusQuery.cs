using MediatR;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetOrderStatus
{
    public class GetOrderStatusQuery : IRequest<GetOrderStatusResponse>
    {
        public Guid OrderId { get; set; }
    }
}
