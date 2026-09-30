using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands;

public sealed record ReturnRentalCommand(Guid RentalId, Guid OperatorId) : IRequest<ServiceOperationResult<RentalReturnDto>>;
public sealed class ReturnRentalValidator : AbstractValidator<ReturnRentalCommand>
{
    public ReturnRentalValidator()
    {
        RuleFor(command => command.RentalId).NotEmpty();
        RuleFor(command => command.OperatorId).NotEmpty();
    }
}
public sealed class ReturnRentalHandler(IServiceOperations operations, IValidator<ReturnRentalCommand> validator) : IRequestHandler<ReturnRentalCommand, ServiceOperationResult<RentalReturnDto>>
{
    public async Task<ServiceOperationResult<RentalReturnDto>> Handle(ReturnRentalCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        return validation.IsValid ? await operations.ReturnRentalAsync(command.RentalId, command.OperatorId, cancellationToken) : ValidationResult<RentalReturnDto>.From(validation);
    }
}
