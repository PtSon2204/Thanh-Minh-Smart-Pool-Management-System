using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands.CreateService
{
    public sealed class CreateServiceHandler : IRequestHandler<CreateServiceCommand, CreateServiceResponse>
    {
        private readonly IServiceOperations _serviceOperations;
        private readonly IValidator<CreateServiceCommand> _validator;

        public CreateServiceHandler(IServiceOperations serviceOperations, IValidator<CreateServiceCommand> validator)
        {
            _serviceOperations = serviceOperations;
            _validator = validator;
        }

        public async Task<CreateServiceResponse> Handle(CreateServiceCommand request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _serviceOperations.CreateServiceAsync(request, cancellationToken);
        }
    }
}
