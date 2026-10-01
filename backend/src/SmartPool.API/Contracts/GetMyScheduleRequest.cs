namespace SmartPool.API.Contracts
{
    public sealed class GetMyScheduleRequest
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public DateOnly? Date { get; set; }
    }
}
