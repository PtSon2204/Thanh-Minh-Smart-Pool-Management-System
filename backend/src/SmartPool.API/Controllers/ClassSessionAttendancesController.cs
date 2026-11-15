using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassSessionAttendances;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/swim-classes/{classId:guid}/sessions/{sessionId:guid}/attendances")]
[Authorize(Roles = "COACH")]
public sealed class ClassSessionAttendancesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission("class_attendances", "read")]
    [ProducesResponseType(typeof(PagedResponse<ClassSessionAttendanceItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetList(Guid classId, Guid sessionId,
        [FromQuery] GetClassSessionAttendancesQuery query,
        CancellationToken cancellationToken)
    {
        query.ClassId = classId;
        query.SessionId = sessionId;
        return ExecuteAsync(async () => Ok(await sender.Send(query, cancellationToken)));
    }

    [HttpPatch("{attendanceId:guid}/review")]
    [RequirePermission("class_attendances", "update")]
    [ProducesResponseType(typeof(ReviewClassAttendanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Review(Guid classId, Guid sessionId, Guid attendanceId,
        [FromBody] ReviewClassAttendanceCommand command,
        CancellationToken cancellationToken)
    {
        command.ClassId = classId;
        command.SessionId = sessionId;
        command.AttendanceId = attendanceId;
        return ExecuteAsync(async () => Ok(await sender.Send(command, cancellationToken)));
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
