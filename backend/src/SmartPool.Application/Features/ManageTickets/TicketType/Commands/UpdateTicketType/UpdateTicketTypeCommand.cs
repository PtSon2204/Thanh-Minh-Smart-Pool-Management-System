using MediatR;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.UpdateTicketType
{
    public class UpdateTicketTypeCommand : IRequest<UpdateTicketTypeResponse>
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TicketCategoryEnum TicketCategory { get; set; }
        public decimal Price { get; set; }
        public int? DurationDays { get; set; }
    }
}
