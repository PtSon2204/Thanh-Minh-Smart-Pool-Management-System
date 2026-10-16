using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.ChangePassword;

public sealed class ChangePasswordCommand : IRequest<IActionResult>
{
    public string CurrentPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmNewPassword { get; init; } = string.Empty;
}