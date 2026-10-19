using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.Refresh;

public sealed class RefreshCommand : IRequest<IActionResult>
{
    public string RefreshToken { get; init; } = string.Empty;
}
