using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed class VerifyRegistrationCommand : IRequest<IActionResult>
{
    public Guid RegistrationId { get; init; }
    public string Otp { get; init; } = string.Empty;
}
