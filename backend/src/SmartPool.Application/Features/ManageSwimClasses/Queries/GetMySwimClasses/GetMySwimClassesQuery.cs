using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMySwimClasses;

public sealed class GetMySwimClassesQuery : IRequest<PagedResponse<MySwimClassItem>>
{
    public string? Status { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
