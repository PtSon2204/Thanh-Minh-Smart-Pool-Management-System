using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands.CheckoutRental
{
    public sealed class CheckoutRentalHandler : IRequestHandler<CheckoutRentalCommand, CheckoutRentalResponse>
    {
        private readonly IServiceOperations _operations;
        private readonly IValidator<CheckoutRentalCommand> _validator;

        public CheckoutRentalHandler(IServiceOperations operations, IValidator<CheckoutRentalCommand> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<CheckoutRentalResponse> Handle(CheckoutRentalCommand request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _operations.CheckoutRentalAsync(request, cancellationToken);
        }
    }
}
