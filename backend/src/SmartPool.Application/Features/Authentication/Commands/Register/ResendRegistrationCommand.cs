using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed class ResendRegistrationCommand : IRequest<IActionResult>
{
    public Guid RegistrationId { get; set; }
}
