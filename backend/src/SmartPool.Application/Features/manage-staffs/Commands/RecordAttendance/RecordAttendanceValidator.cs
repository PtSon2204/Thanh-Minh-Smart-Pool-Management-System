using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance
{
    public sealed class RecordAttendanceValidator : AbstractValidator<RecordAttendanceCommand>
    {
        public RecordAttendanceValidator()
        {
            RuleFor(item => item.Id).NotEmpty().WithMessage("Mã lịch làm việc không được để trống.");
        }
    }
}
