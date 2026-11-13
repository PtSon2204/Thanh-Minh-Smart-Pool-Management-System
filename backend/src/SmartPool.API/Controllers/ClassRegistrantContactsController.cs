using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrantContact;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "COACH")]
public sealed class ClassRegistrantContactsController(ISender sender) : ControllerBase
{
    [HttpGet("{userId:guid}/class-contact")]
    [RequirePermission("class_registrations", "read")]
    [ProducesResponseType(typeof(GetClassRegistrantContactResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContact(Guid userId, [FromQuery] Guid classId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new GetClassRegistrantContactQuery(classId, userId),
                cancellationToken));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new
            {
                message = "Dữ liệu không hợp lệ.",
                errors = exception.Errors.GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray())
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }
}
