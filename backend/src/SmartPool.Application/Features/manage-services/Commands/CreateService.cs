using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands;

public sealed record CreateServiceCommand(CreateServiceRequest Request) : IRequest<ServiceOperationResult<ServiceDto>>;
public sealed class CreateServiceValidator : AbstractValidator<CreateServiceCommand>
{
    public CreateServiceValidator()
    {
        RuleFor(command => command.Request.Name).NotEmpty().MaximumLength(255);
        RuleFor(command => command.Request.Type).Must(type => type is "Sale" or "Rental");
        RuleFor(command => command.Request.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Request.StockQuantity).GreaterThanOrEqualTo(0).When(command => command.Request.StockQuantity.HasValue);
    }
}
public sealed class CreateServiceHandler(IServiceOperations operations, IValidator<CreateServiceCommand> validator) : IRequestHandler<CreateServiceCommand, ServiceOperationResult<ServiceDto>>
{
    public async Task<ServiceOperationResult<ServiceDto>> Handle(CreateServiceCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        return validation.IsValid ? await operations.CreateServiceAsync(command.Request, cancellationToken) : ValidationResult<ServiceDto>.From(validation);
    }
}
internal static class ValidationResult<T>
{
    public static ServiceOperationResult<T> From(FluentValidation.Results.ValidationResult validation) =>
        new(default, ServiceOperationError.Validation, validation.Errors.GroupBy(error => error.PropertyName).ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()));
}
