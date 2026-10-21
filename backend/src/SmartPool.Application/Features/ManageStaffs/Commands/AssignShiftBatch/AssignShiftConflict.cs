namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch
{
    public sealed record AssignShiftConflict(
        Guid EmployeeId,
        Guid ShiftId,
        DateOnly WorkDate,
        string Code,
        string Message);
}
