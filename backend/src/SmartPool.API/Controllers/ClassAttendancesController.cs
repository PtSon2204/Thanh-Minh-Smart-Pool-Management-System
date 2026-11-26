using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassAttendances;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/swim-classes/{classId:guid}")]
[Authorize(Roles = "CUSTOMER")]
public sealed class ClassAttendancesController(ISender sender) : ControllerBase
{
    [HttpGet("attendances/me")]
    [RequirePermission("class_attendances", "read")]
    [ProducesResponseType(typeof(PagedResponse<MyClassAttendanceItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetMine(Guid classId,
        [FromQuery] GetMyClassAttendancesQuery query,
        CancellationToken cancellationToken)
    {
        query.ClassId = classId;
        return ExecuteAsync(async () => Ok(await sender.Send(query, cancellationToken)));
    }

    private static async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> operation)
    {
        try
        {
            return await operation();
        }
        catch (ValidationException exception)
        {
            return new BadRequestObjectResult(new
            {
                message = "Dữ liệu điểm danh không hợp lệ.",
                errors = exception.Errors.GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray())
            });
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException exception)
        {
            return new NotFoundObjectResult(new { message = exception.Message });
        }
        catch (SwimClassStateConflictException exception)
        {
            return new ConflictObjectResult(new { message = exception.Message });
        }
    }
}
