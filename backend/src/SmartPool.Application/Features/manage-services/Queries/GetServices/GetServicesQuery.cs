using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageServices.Queries.GetServices
{
    public sealed class GetServicesQuery : IRequest<PagedResponse<GetServicesResponse>>
    {
        public string? SearchTerm { get; set; }
        public string? Type { get; set; }
        public bool? IsActive { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
