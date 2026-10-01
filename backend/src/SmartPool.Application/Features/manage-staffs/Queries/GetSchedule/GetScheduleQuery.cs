using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule
{
    public sealed class GetScheduleQuery : IRequest<PagedResponse<GetScheduleResponse>>
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public Guid? EmployeeId { get; set; }
        public DateOnly? Date { get; set; }
        public string? Status { get; set; }
        [BindNever]
        public bool RequireEmployee { get; set; }
    }
}
