namespace SmartPool.Application.Features.AccessControlPool.Queries.GetDailyEntrySummary
{
    public class GetDailyEntrySummaryResponse
    {
        public DateOnly Date { get; set; }
        public int AcceptedEntries { get; set; }
    }
}
