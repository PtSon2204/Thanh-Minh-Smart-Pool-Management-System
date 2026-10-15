using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch
{
    public sealed class AssignShiftBatchValidator : AbstractValidator<AssignShiftBatchCommand>
    {
        public AssignShiftBatchValidator()
        {
            RuleFor(command => command.Assignments)
                .NotEmpty().WithMessage("Danh sách phân công không được để trống.")
                .Must(assignments => assignments is null || assignments.Count == 0 ||
                    assignments.Max(item => item.WorkDate).DayNumber -
                    assignments.Min(item => item.WorkDate).DayNumber <= 6)
                .WithMessage("Danh sách phân công không được vượt quá bảy ngày liên tiếp.");
            RuleForEach(command => command.Assignments).ChildRules(assignment =>
            {
                assignment.RuleFor(item => item.EmployeeId).NotEmpty().WithMessage("Mã nhân viên không được để trống.");
                assignment.RuleFor(item => item.ShiftId).NotEmpty().WithMessage("Mã ca làm việc không được để trống.");
                assignment.RuleFor(item => item.WorkDate).NotEmpty().WithMessage("Ngày làm việc không được để trống.");
            });
        }
    }
}
