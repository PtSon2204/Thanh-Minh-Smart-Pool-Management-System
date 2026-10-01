using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffs
{
    public sealed class GetStaffsQuery : IRequest<PagedResponse<GetStaffsResponse>>
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SearchTerm { get; set; }
        public string? Status { get; set; }
    }
}
