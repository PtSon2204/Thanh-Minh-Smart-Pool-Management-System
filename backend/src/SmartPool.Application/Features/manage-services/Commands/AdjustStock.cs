using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands;

public sealed record AdjustStockCommand(Guid ServiceId, AdjustStockRequest Request, Guid OperatorId) : IRequest<ServiceOperationResult<StockAdjustmentDto>>;
public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(command => command.ServiceId).NotEmpty();
        RuleFor(command => command.OperatorId).NotEmpty();
        RuleFor(command => command.Request.Delta).NotEqual(0);
        RuleFor(command => command.Request.Note).NotEmpty();
    }
}
public sealed class AdjustStockHandler(IServiceOperations operations, IValidator<AdjustStockCommand> validator) : IRequestHandler<AdjustStockCommand, ServiceOperationResult<StockAdjustmentDto>>
{
    public async Task<ServiceOperationResult<StockAdjustmentDto>> Handle(AdjustStockCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        return validation.IsValid ? await operations.AdjustStockAsync(command.ServiceId, command.Request, command.OperatorId, cancellationToken) : ValidationResult<StockAdjustmentDto>.From(validation);
    }
}
