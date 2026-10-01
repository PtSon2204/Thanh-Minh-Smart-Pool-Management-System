using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.Application.Features.AccessControlPool.Queries.GetEntryHistory
{
    public class GetEntryHistoryQuery : IRequest<PoolAccessValidationResult<PagedResponse<GetEntryHistoryResponse>>>
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public DateOnly? Date { get; set; }
        public string? Status { get; set; }
        public Guid? TicketId { get; set; }
    }
}
