using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetSalaries
{
    public sealed class GetSalariesQuery : IRequest<PagedResponse<GetSalariesResponse>>
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public Guid? EmployeeId { get; set; }
        public int? Month { get; set; }
        public int? Year { get; set; }
    }
}
