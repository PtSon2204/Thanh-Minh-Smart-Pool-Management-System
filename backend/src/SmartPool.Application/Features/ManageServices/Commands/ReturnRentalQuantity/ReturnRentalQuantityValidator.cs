using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;

public sealed class ReturnRentalQuantityValidator : AbstractValidator<ReturnRentalQuantityCommand>
{
    public ReturnRentalQuantityValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.OperatorId).NotEmpty();
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.SettlementConfirmed).Equal(true).WithMessage("Xác nhận hoàn cọc và ghi nhận cọc giữ lại.");
        RuleFor(command => command.RentalIds).NotNull();
        RuleFor(command => command.DamagedRentalIds).NotNull();
        RuleFor(command => command.LostRentalIds).NotNull();
        RuleFor(command => command.SelectedIds).Must(ids => ids.Count is >= 1 and <= 100 && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .WithMessage("Chọn từ 1 đến 100 lượt thuê khác nhau; các nhóm tình trạng không được trùng nhau.").OverridePropertyName(nameof(ReturnRentalQuantityCommand.RentalIds));
        RuleFor(command => command.Reason).MaximumLength(500);
        When(command => (command.DamagedRentalIds?.Count ?? 0) + (command.LostRentalIds?.Count ?? 0) > 0,
            () => RuleFor(command => command.Reason).NotEmpty().WithMessage("Nhập lý do hỏng hoặc mất."));
    }
}
