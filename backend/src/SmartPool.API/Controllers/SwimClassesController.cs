using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/swim-classes")]
public sealed class SwimClassesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("swim_classes", "create")]
    [ProducesResponseType(typeof(CreateSwimClassResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSwimClassCommand command,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _)) return Forbid();

        try
        {
            var created = await sender.Send(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ValidationException exception)
        {
            return BadRequest(new
            {
                message = "Dữ liệu lớp không hợp lệ.",
                errors = exception.Errors.GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray())
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
