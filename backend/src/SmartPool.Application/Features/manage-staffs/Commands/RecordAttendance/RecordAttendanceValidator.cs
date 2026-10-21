using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance
{
    public sealed class RecordAttendanceValidator : AbstractValidator<RecordAttendanceCommand>
    {
        public RecordAttendanceValidator()
        {
            RuleFor(item => item.Id).NotEmpty().WithMessage("Mã lịch làm việc không được để trống.");
            RuleFor(item => item.Method)
                .Must(method => method is "Manual" or "QR")
                .WithMessage("Phương thức chấm công không hợp lệ.");
            RuleFor(item => item.PerformedByUserId).NotEmpty().WithMessage("Người thực hiện không hợp lệ.");
            When(item => item.Method == "Manual", () =>
            {
                RuleFor(item => item.Reason)
                    .NotEmpty().WithMessage("Lý do chấm công thủ công không được để trống.")
                    .MaximumLength(500).WithMessage("Lý do chấm công thủ công không được vượt quá 500 ký tự.");
            });
            When(item => item.Method == "QR", () =>
            {
                RuleFor(item => item.Reason).Empty().WithMessage("Chấm công QR không nhận lý do thủ công.");
            });
        }
    }
}
