using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff
{
    public sealed class UpdateStaffValidator : AbstractValidator<UpdateStaffCommand>
    {
        public UpdateStaffValidator()
        {
            RuleFor(item => item.UserId).NotEmpty().WithMessage("Mã nhân viên không được để trống.");
            When(item => item.Details is null, () =>
                RuleFor(item => item.Status).Must(value => value is "Working" or "Inactive").WithMessage("Trạng thái nhân viên không hợp lệ."));
            When(item => item.Details is not null, () =>
            {
                RuleFor(item => item.ActorId).NotEmpty();
                RuleFor(item => item.HasEditSnapshot).Equal(true).WithMessage("Hãy tải lại hồ sơ trước khi lưu thay đổi.");
            });
        }
    }
}
