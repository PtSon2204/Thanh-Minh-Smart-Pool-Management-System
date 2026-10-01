using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.Application.Features.AccessControlPool.Queries.GetDailyEntrySummary
{
    public class GetDailyEntrySummaryQuery : IRequest<PoolAccessValidationResult<GetDailyEntrySummaryResponse>>
    {
        public DateOnly Date { get; set; }
    }
}
