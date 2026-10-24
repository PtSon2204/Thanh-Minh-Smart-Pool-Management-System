using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.GoogleRegister;

public sealed class GoogleRegisterCommand : IRequest<IActionResult>
{
    public string Credential { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}
