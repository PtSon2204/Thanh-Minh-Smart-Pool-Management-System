using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Queries.GetServices
{
    public sealed class GetServicesHandler : IRequestHandler<GetServicesQuery, PagedResponse<GetServicesResponse>>
    {
        private readonly IServiceOperations _serviceOperations;
        private readonly IValidator<GetServicesQuery> _validator;

        public GetServicesHandler(IServiceOperations serviceOperations, IValidator<GetServicesQuery> validator)
        {
            _serviceOperations = serviceOperations;
            _validator = validator;
        }

        public async Task<PagedResponse<GetServicesResponse>> Handle(GetServicesQuery request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _serviceOperations.GetServicesAsync(request, cancellationToken);
        }
    }
}
