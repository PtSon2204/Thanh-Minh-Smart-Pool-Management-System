using MediatR;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.ToggleLockTicketType
{
    public class ToggleLockTicketTypeCommand : IRequest<bool>
    {
        public Guid Id { get; set; }
    }
}
