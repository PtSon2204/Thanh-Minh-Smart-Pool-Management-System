using MediatR;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetTicketTypeById
{
    public class GetTicketTypeByIdQuery : IRequest<GetTicketTypeByIdResponse>
    {
        public Guid Id { get; set; }
    }
}
