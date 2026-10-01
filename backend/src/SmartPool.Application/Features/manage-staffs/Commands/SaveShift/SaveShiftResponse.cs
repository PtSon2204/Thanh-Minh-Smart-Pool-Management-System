namespace SmartPool.Application.Features.ManageStaffs.Commands.SaveShift
{
    public sealed class SaveShiftResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public bool IsActive { get; set; }
    }
}
