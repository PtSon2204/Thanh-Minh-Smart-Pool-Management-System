using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;

public sealed class ReturnRentalQuantityHandler(
    IServiceOperations operations,
    IValidator<ReturnRentalQuantityCommand> validator)
    : IRequestHandler<ReturnRentalQuantityCommand, ReturnRentalQuantityResponse>
{
    public async Task<ReturnRentalQuantityResponse> Handle(
        ReturnRentalQuantityCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        return await operations.ReturnRentalQuantityAsync(request, cancellationToken);
    }
}
