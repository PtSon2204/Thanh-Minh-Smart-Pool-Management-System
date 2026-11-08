using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassSessionAttendances;

public sealed class GetClassSessionAttendancesQuery
    : IRequest<PagedResponse<ClassSessionAttendanceItem>>
{
    public Guid ClassId { get; set; }
    public Guid SessionId { get; set; }
    public string? Status { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
