using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed record VerifyPasswordResetCommand(Guid RecoveryId, string Otp) : IRequest<IActionResult>
{
    public override string ToString() => $"VerifyPasswordResetCommand {{ RecoveryId = {RecoveryId}, Otp = [REDACTED] }}";
}

public sealed record VerifyPasswordResetResponse(string ResetToken, DateTimeOffset ResetTokenExpiresAtUtc)
{
    public override string ToString() => "VerifyPasswordResetResponse { ResetToken = [REDACTED] }";
}
