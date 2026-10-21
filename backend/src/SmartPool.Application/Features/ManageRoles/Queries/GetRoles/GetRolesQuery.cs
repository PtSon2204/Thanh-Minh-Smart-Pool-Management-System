using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.ManageRoles.Queries.GetRoles;

public sealed record GetRolesQuery : IRequest<IActionResult>;
