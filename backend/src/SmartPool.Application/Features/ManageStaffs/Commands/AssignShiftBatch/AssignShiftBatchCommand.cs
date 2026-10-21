using MediatR;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch
{
    public sealed class AssignShiftBatchCommand : IRequest<IReadOnlyList<AssignShiftResponse>>
    {
        public IReadOnlyList<AssignShiftBatchItem> Assignments { get; init; } = [];
    }
}
