using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry
{
    public class ConfirmEntryResponse
    {
        public bool Allowed { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public TicketSummaryResponse? Ticket { get; set; }
        public DateTime ScanTime { get; set; }
        public string InputMode { get; set; } = string.Empty;
    }
}
