using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;

public sealed class UpdateSwimClassValidator : AbstractValidator<UpdateSwimClassCommand>
{
    public UpdateSwimClassValidator()
    {
        RuleFor(command => command.ClassId).NotEmpty();
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Tên lớp không được để trống.")
            .Must(name => name is null || name.Trim().Length <= 150)
            .WithMessage("Tên lớp không được vượt quá 150 ký tự.");
        RuleFor(command => command.Capacity).GreaterThan(0).WithMessage("Sức chứa phải lớn hơn 0.");
        RuleFor(command => command.Fee)
            .GreaterThanOrEqualTo(0).WithMessage("Học phí không được âm.")
            .PrecisionScale(18, 2, true).WithMessage("Học phí tối đa 18 chữ số, gồm 2 chữ số thập phân.");
        RuleFor(command => command.ExpectedUpdatedAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("Cần thời gian cập nhật gần nhất của lớp.");
        RuleFor(command => command.RegistrationStartAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("Thời gian bắt đầu đăng ký không hợp lệ.");
        RuleFor(command => command.RegistrationEndAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("Thời gian kết thúc đăng ký không hợp lệ.")
            .GreaterThan(command => command.RegistrationStartAt)
            .WithMessage("Thời gian kết thúc đăng ký phải sau thời gian bắt đầu.");
    }
}
