using MediatR;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.DeleteTicketType
{
    public class DeleteTicketTypeCommand : IRequest<bool>
    {
        public Guid Id { get; set; }
    }
}
