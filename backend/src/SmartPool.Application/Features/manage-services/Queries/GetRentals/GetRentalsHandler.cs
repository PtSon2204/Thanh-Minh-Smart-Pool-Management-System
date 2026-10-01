using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Queries.GetRentals
{
    public sealed class GetRentalsHandler : IRequestHandler<GetRentalsQuery, PagedResponse<GetRentalsResponse>>
    {
        private readonly IServiceOperations _operations;
        private readonly IValidator<GetRentalsQuery> _validator;

        public GetRentalsHandler(IServiceOperations operations, IValidator<GetRentalsQuery> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<PagedResponse<GetRentalsResponse>> Handle(GetRentalsQuery request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _operations.GetRentalsAsync(request, cancellationToken);
        }
    }
}
