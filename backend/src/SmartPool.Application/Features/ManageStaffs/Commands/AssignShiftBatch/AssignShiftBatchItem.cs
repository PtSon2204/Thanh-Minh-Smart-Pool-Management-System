using MediatR;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch
{

    public sealed class AssignShiftBatchItem
    {
        public Guid EmployeeId { get; init; }
        public Guid ShiftId { get; init; }
        public DateOnly WorkDate { get; init; }
    }
}
