using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands;

public sealed record UpdateServiceCommand(Guid ServiceId, UpdateServiceRequest Request) : IRequest<ServiceOperationResult<ServiceDto>>;
public sealed class UpdateServiceValidator : AbstractValidator<UpdateServiceCommand>
{
    public UpdateServiceValidator()
    {
        RuleFor(command => command.ServiceId).NotEmpty();
        RuleFor(command => command.Request.Name).NotEmpty().MaximumLength(255);
        RuleFor(command => command.Request.Type).Must(type => type is "Sale" or "Rental");
        RuleFor(command => command.Request.Price).GreaterThanOrEqualTo(0);
    }
}
public sealed class UpdateServiceHandler(IServiceOperations operations, IValidator<UpdateServiceCommand> validator) : IRequestHandler<UpdateServiceCommand, ServiceOperationResult<ServiceDto>>
{
    public async Task<ServiceOperationResult<ServiceDto>> Handle(UpdateServiceCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        return validation.IsValid ? await operations.UpdateServiceAsync(command.ServiceId, command.Request, cancellationToken) : ValidationResult<ServiceDto>.From(validation);
    }
}
