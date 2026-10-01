using FluentValidation;
using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.AccessControlPool.Queries.GetDailyEntrySummary
{
    public class GetDailyEntrySummaryHandler : IRequestHandler<GetDailyEntrySummaryQuery, PoolAccessValidationResult<GetDailyEntrySummaryResponse>>
    {
        private readonly IPoolAccessOperations _operations;
        private readonly IValidator<GetDailyEntrySummaryQuery> _validator;

        public GetDailyEntrySummaryHandler(IPoolAccessOperations operations, IValidator<GetDailyEntrySummaryQuery> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<PoolAccessValidationResult<GetDailyEntrySummaryResponse>> Handle(GetDailyEntrySummaryQuery request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                var failure = validation.Errors[0];
                return PoolAccessValidationResult<GetDailyEntrySummaryResponse>.Failure(failure.PropertyName, failure.ErrorMessage);
            }

            var result = await _operations.GetDailySummaryAsync(request.Date, cancellationToken);
            return PoolAccessValidationResult<GetDailyEntrySummaryResponse>.Success(result);
        }
    }
}
