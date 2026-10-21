namespace SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule
{
    public sealed class GetScheduleResponse
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public Guid ShiftId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public bool IsShiftActive { get; set; }
        public DateOnly WorkDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string? CheckInMethod { get; set; }
        public string? CheckOutMethod { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
