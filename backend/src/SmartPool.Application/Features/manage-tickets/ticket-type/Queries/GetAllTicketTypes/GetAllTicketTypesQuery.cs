using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes
{
    public class GetAllTicketTypesQuery : IRequest<PagedResponse<GetAllTicketTypesResponse>>
    {
        public string? SearchTerm { get; set; }
        public TicketCategoryEnum? Category { get; set; }
        public bool? IsActive { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
