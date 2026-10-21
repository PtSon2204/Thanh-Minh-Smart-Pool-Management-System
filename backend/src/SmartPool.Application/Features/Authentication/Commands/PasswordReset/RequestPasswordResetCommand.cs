using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : IRequest<IActionResult>;
