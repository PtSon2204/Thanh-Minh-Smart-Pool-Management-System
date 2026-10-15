namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch
{
    public sealed record AssignShiftConflict(
        Guid EmployeeId,
        Guid ShiftId,
        DateOnly WorkDate,
        string Code,
        string Message);

    public sealed class AssignShiftBatchConflictException : Exception
    {
        public AssignShiftBatchConflictException(IReadOnlyList<AssignShiftConflict> conflicts)
            : base("Một hoặc nhiều phân công không hợp lệ.")
        {
            Conflicts = conflicts;
        }

        public IReadOnlyList<AssignShiftConflict> Conflicts { get; }
    }
}
