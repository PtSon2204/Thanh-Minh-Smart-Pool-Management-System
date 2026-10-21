using FluentValidation;
using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry
{
    public class ConfirmEntryValidator : AbstractValidator<ConfirmEntryCommand>
    {
        public ConfirmEntryValidator()
        {
            RuleFor(command => command.Code)
                .NotEmpty().WithMessage("Mã vé không được để trống.")
                .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Mã vé không hợp lệ.")
                .MaximumLength(255).WithMessage("Mã vé không được vượt quá 255 ký tự.");
            RuleFor(command => command.InputMode)
                .Must(mode => mode is PoolAccessValues.Manual or PoolAccessValues.Qr)
                .WithMessage("Phương thức quét không hợp lệ.");
            RuleFor(command => command.OperatorId)
                .NotEmpty().WithMessage("Nhân viên xác nhận không hợp lệ.");
        }
    }
}
