using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.AccessControlPool.Queries.GetEntryHistory
{
    public class GetEntryHistoryHandler : IRequestHandler<GetEntryHistoryQuery, PoolAccessValidationResult<PagedResponse<GetEntryHistoryResponse>>>
    {
        private readonly IPoolAccessOperations _operations;
        private readonly IValidator<GetEntryHistoryQuery> _validator;

        public GetEntryHistoryHandler(IPoolAccessOperations operations, IValidator<GetEntryHistoryQuery> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<PoolAccessValidationResult<PagedResponse<GetEntryHistoryResponse>>> Handle(GetEntryHistoryQuery request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                var failure = validation.Errors[0];
                return PoolAccessValidationResult<PagedResponse<GetEntryHistoryResponse>>.Failure(failure.PropertyName, failure.ErrorMessage);
            }

            var result = await _operations.GetHistoryAsync(request, cancellationToken);
            return PoolAccessValidationResult<PagedResponse<GetEntryHistoryResponse>>.Success(result);
        }
    }
}
