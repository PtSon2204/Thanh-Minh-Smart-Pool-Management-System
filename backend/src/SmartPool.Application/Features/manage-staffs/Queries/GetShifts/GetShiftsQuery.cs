using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetShifts
{
    public sealed class GetShiftsQuery : IRequest<PagedResponse<GetShiftsResponse>>
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public bool? IsActive { get; set; }
    }
}
