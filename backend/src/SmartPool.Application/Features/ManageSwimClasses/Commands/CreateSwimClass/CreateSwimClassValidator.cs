using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;

public sealed class CreateSwimClassValidator : AbstractValidator<CreateSwimClassCommand>
{
    public CreateSwimClassValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Tên lớp không được để trống.")
            .Must(name => name is null || name.Trim().Length <= 150)
            .WithMessage("Tên lớp không được vượt quá 150 ký tự.");
        RuleFor(x => x.Capacity).GreaterThan(0).WithMessage("Sức chứa phải lớn hơn 0.");
        RuleFor(x => x.Fee)
            .GreaterThanOrEqualTo(0).WithMessage("Học phí không được âm.")
            .PrecisionScale(18, 2, true).WithMessage("Học phí tối đa 18 chữ số, gồm 2 chữ số thập phân.");
        RuleFor(x => x.CoachUserId).NotEmpty().WithMessage("Coach phụ trách không được để trống.");
        RuleFor(x => x.RegistrationStartAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("Thời gian bắt đầu đăng ký không hợp lệ.");
        RuleFor(x => x.RegistrationEndAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("Thời gian kết thúc đăng ký không hợp lệ.")
            .GreaterThan(x => x.RegistrationStartAt)
            .WithMessage("Thời gian kết thúc đăng ký phải sau thời gian bắt đầu.");
    }
}
