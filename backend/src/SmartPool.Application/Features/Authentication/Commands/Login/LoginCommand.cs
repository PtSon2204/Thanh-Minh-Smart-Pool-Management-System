using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.Login;

public sealed class LoginCommand : IRequest<IActionResult>
{
    public string Identifier { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
