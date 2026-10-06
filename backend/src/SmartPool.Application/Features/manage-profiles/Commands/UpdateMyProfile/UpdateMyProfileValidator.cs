using FluentValidation;

namespace SmartPool.Application.Features.Profiles.Commands.UpdateMyProfile;

public sealed class UpdateMyProfileValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(255).WithMessage("Họ tên không được vượt quá 255 ký tự.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự.");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Ngày sinh không hợp lệ.");
    }
}
