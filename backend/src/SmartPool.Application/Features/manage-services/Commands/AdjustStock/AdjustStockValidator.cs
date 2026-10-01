using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.AdjustStock
{
    public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
    {
        public AdjustStockValidator()
        {
            RuleFor(command => command.ServiceId).NotEmpty().WithMessage("Mã dịch vụ không hợp lệ.");
            RuleFor(command => command.OperatorId).NotEmpty().WithMessage("Mã nhân viên không hợp lệ.");
            RuleFor(command => command.Delta).NotEqual(0).WithMessage("Số lượng điều chỉnh phải khác 0.");
            RuleFor(command => command.Note).NotEmpty().WithMessage("Ghi chú điều chỉnh không được để trống.");
        }
    }
}
