using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.AccessControlPool.Contracts
{
    public class TicketSummaryResponse
    {
        public Guid Id { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public TicketCategoryEnum? Category { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Status { get; set; }
        public int? RemainingEntries { get; set; }
    }
}
