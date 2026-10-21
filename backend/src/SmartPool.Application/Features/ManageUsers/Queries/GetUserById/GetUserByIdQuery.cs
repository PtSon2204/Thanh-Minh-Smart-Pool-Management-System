using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.ManageUsers.Queries.GetUserById;

public sealed class GetUserByIdQuery : IRequest<IActionResult>
{
    public Guid Id { get; init; }
}
