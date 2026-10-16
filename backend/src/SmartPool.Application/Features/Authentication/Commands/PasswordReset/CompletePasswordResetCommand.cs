using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed record CompletePasswordResetCommand(
    Guid RecoveryId, string ResetToken, string NewPassword, string ConfirmPassword) : IRequest<IActionResult>
{
    public override string ToString() =>
        $"CompletePasswordResetCommand {{ RecoveryId = {RecoveryId}, secrets = [REDACTED] }}";
}
