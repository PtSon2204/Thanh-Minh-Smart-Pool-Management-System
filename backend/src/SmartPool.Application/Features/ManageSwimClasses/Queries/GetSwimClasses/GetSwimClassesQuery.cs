using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;

public sealed class GetSwimClassesQuery : IRequest<PagedResponse<SwimClassListItem>>
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? Scope { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
