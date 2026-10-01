using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory
{
    public sealed class GetInventoryHistoryQuery : IRequest<PagedResponse<GetInventoryHistoryResponse>>
    {
        [BindNever]
        public Guid ServiceId { get; set; }
        public DateOnly? Date { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
