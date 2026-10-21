namespace SmartPool.Application.Features.AccessControlPool.Queries.GetEntryHistory
{
    public class GetEntryHistoryResponse
    {
        public Guid Id { get; set; }
        public Guid? TicketId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime ScanTime { get; set; }
        public Guid? OperatorId { get; set; }
        public string? InputMode { get; set; }
    }
}
