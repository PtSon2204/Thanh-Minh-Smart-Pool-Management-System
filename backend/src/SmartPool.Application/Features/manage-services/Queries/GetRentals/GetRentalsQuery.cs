using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageServices.Queries.GetRentals
{
    public sealed class GetRentalsQuery : IRequest<PagedResponse<GetRentalsResponse>>
    {
        public string? Status { get; set; }
        public DateOnly? Date { get; set; }
        public Guid? ProductId { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
