using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.Logout;

public sealed class LogoutCommand : IRequest<IActionResult>
{
    public string RefreshToken { get; init; } = string.Empty;
}
