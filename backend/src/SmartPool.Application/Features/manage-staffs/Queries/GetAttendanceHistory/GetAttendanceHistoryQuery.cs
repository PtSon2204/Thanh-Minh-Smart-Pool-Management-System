using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetAttendanceHistory;

public sealed class GetAttendanceHistoryQuery : IRequest<PagedResponse<GetAttendanceHistoryResponse>>
{
    public Guid AssignmentId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
