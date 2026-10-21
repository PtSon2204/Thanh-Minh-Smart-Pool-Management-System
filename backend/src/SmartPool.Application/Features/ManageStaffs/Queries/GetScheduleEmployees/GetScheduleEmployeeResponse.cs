namespace SmartPool.Application.Features.ManageStaffs.Queries.GetScheduleEmployees
{
    public sealed class GetScheduleEmployeeResponse
    {
        public Guid EmployeeId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string EmploymentStatus { get; init; } = string.Empty;
    }
}
