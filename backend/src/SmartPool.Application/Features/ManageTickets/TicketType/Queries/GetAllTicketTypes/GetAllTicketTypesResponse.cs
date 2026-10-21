using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes
{
    public class GetAllTicketTypesResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public TicketCategoryEnum TicketCategory { get; set; }

        public decimal Price { get; set; }

        public int? DurationDays { get; set; }

        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
