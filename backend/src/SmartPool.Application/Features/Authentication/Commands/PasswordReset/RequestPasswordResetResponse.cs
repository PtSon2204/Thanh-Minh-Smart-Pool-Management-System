using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed record RequestPasswordResetResponse(
    Guid RecoveryId,
    DateTimeOffset OtpExpiresAtUtc,
    DateTimeOffset RecoveryExpiresAtUtc,
    DateTimeOffset ResendAvailableAtUtc);
