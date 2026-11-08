using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassAttendances;

public sealed class GetMyClassAttendancesQuery : IRequest<PagedResponse<MyClassAttendanceItem>>
{
    public Guid ClassId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
