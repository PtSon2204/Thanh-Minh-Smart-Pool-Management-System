using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers
{
    public sealed class GetAllVouchersQuery : IRequest<PagedResponse<GetVouchersResponse>>
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
    }
}
