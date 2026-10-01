using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRental
{
    public sealed class ReturnRentalHandler : IRequestHandler<ReturnRentalCommand, ReturnRentalResponse>
    {
        private readonly IServiceOperations _operations;
        private readonly IValidator<ReturnRentalCommand> _validator;

        public ReturnRentalHandler(IServiceOperations operations, IValidator<ReturnRentalCommand> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<ReturnRentalResponse> Handle(ReturnRentalCommand request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _operations.ReturnRentalAsync(request, cancellationToken);
        }
    }
}
