using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory
{
    public sealed class GetInventoryHistoryHandler : IRequestHandler<GetInventoryHistoryQuery, PagedResponse<GetInventoryHistoryResponse>>
    {
        private readonly IServiceOperations _operations;
        private readonly IValidator<GetInventoryHistoryQuery> _validator;

        public GetInventoryHistoryHandler(IServiceOperations operations, IValidator<GetInventoryHistoryQuery> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<PagedResponse<GetInventoryHistoryResponse>> Handle(GetInventoryHistoryQuery request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _operations.GetInventoryHistoryAsync(request, cancellationToken);
        }
    }
}
