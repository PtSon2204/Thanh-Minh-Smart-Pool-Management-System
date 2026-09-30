using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands;

public sealed record CheckoutRentalCommand(RentalCheckoutRequest Request, Guid OperatorId) : IRequest<ServiceOperationResult<RentalCheckoutDto>>;
public sealed class CheckoutRentalValidator : AbstractValidator<CheckoutRentalCommand>
{
    public CheckoutRentalValidator()
    {
        RuleFor(command => command.OperatorId).NotEmpty();
        RuleFor(command => command.Request.ProductId).NotEmpty();
        RuleFor(command => command.Request.Quantity).GreaterThan(0);
        RuleFor(command => command.Request.DepositPerUnit).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Request.CustomerName).MaximumLength(255).When(command => command.Request.CustomerName is not null);
        RuleFor(command => command.Request.CustomerPhone).MaximumLength(20).When(command => command.Request.CustomerPhone is not null);
    }
}
public sealed class CheckoutRentalHandler(IServiceOperations operations, IValidator<CheckoutRentalCommand> validator) : IRequestHandler<CheckoutRentalCommand, ServiceOperationResult<RentalCheckoutDto>>
{
    public async Task<ServiceOperationResult<RentalCheckoutDto>> Handle(CheckoutRentalCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        return validation.IsValid ? await operations.CheckoutRentalAsync(command.Request, command.OperatorId, cancellationToken) : ValidationResult<RentalCheckoutDto>.From(validation);
    }
}
