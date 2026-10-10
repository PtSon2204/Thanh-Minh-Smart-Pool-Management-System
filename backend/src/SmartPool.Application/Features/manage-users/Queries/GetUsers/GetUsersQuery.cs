using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.ManageUsers.Queries.GetUsers;

public sealed class GetUsersQuery : IRequest<IActionResult>
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
}
