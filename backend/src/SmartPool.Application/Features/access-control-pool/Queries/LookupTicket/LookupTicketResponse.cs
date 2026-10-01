using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket
{
    public class LookupTicketResponse
    {
        public bool Found { get; set; }
        public bool IsValid { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public TicketSummaryResponse? Ticket { get; set; }
    }
}
