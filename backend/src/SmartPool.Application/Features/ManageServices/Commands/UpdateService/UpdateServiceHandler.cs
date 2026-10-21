using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands.UpdateService
{
    public sealed class UpdateServiceHandler : IRequestHandler<UpdateServiceCommand, UpdateServiceResponse>
    {
        private readonly IServiceOperations _operations;
        private readonly IValidator<UpdateServiceCommand> _validator;

        public UpdateServiceHandler(IServiceOperations operations, IValidator<UpdateServiceCommand> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<UpdateServiceResponse> Handle(UpdateServiceCommand request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _operations.UpdateServiceAsync(request, cancellationToken);
        }
    }
}
