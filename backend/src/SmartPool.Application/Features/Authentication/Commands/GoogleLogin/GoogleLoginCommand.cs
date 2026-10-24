using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.GoogleLogin;

public sealed class GoogleLoginCommand : IRequest<IActionResult>
{
    public string Credential { get; init; } = string.Empty;
}
