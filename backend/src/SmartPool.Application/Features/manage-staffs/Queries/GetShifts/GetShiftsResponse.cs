namespace SmartPool.Application.Features.ManageStaffs.Queries.GetShifts
{
    public sealed class GetShiftsResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public bool IsActive { get; set; }
    }
}
