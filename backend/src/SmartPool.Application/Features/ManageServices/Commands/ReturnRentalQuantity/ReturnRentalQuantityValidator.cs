using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;

public sealed class ReturnRentalQuantityValidator : AbstractValidator<ReturnRentalQuantityCommand>
{
    public ReturnRentalQuantityValidator()
    {
        RuleFor(command => command.RentalIds).NotNull().NotEmpty();
        RuleFor(command => command.RentalIds)
            .Must(ids => ids is null || ids.Count <= 100)
            .WithMessage("A return request cannot include more than 100 rental identifiers.");
        RuleFor(command => command.RentalIds)
            .Must(ids => ids is null || ids.All(id => id != Guid.Empty))
            .WithMessage("Rental identifiers must be non-empty GUIDs.");
        RuleFor(command => command.RentalIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Rental identifiers must be distinct.");
    }
}
