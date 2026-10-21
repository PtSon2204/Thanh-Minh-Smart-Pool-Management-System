using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed record ResendPasswordResetCommand(Guid RecoveryId) : IRequest<IActionResult>;
