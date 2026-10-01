using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment
{
    public sealed class CancelShiftAssignmentValidator : AbstractValidator<CancelShiftAssignmentCommand>
    {
        public CancelShiftAssignmentValidator()
        {
            RuleFor(item => item.Id).NotEmpty().WithMessage("Mã lịch làm việc không được để trống.");
        }
    }
}
