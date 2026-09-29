using MediatR;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType
{
    public class CreateTicketTypeCommand : IRequest<CreateTicketTypeResponse>
    {
        public string Name { get; set; } = string.Empty;

        public TicketCategoryEnum TicketCategory { get; set; }

        public decimal Price { get; set; }

        public int? DurationDays { get; set; }
    }
}
