using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands.AdjustStock
{
    public sealed class AdjustStockHandler : IRequestHandler<AdjustStockCommand, AdjustStockResponse>
    {
        private readonly IServiceOperations _operations;
        private readonly IValidator<AdjustStockCommand> _validator;

        public AdjustStockHandler(IServiceOperations operations, IValidator<AdjustStockCommand> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<AdjustStockResponse> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _operations.AdjustStockAsync(request, cancellationToken);
        }
    }
}
