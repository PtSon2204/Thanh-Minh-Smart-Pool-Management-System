using MediatR;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShift
{
    public sealed class AssignShiftCommand : IRequest<AssignShiftResponse>
    {
        public Guid EmployeeId { get; set; }
        public Guid ShiftId { get; set; }
        public DateOnly WorkDate { get; set; }
    }
}
