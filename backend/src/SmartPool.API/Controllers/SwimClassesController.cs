using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ActivateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;
using SmartPool.Application.Common.Models;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/swim-classes")]
public sealed class SwimClassesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [RequirePermission("swim_classes", "read")]
    [ProducesResponseType(typeof(PagedResponse<SwimClassListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] GetSwimClassesQuery query,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _)) return Forbid();
        return await ExecuteAsync(async () => Ok(await sender.Send(query, cancellationToken)));
    }

    [HttpGet("{classId:guid}")]
    [Authorize]
    [RequirePermission("swim_classes", "read")]
    [ProducesResponseType(typeof(SwimClassDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid classId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _)) return Forbid();
        return await ExecuteAsync(async () =>
            Ok(await sender.Send(new GetSwimClassByIdQuery(classId), cancellationToken)));
    }

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
        return await ExecuteAsync(async () =>
            StatusCode(StatusCodes.Status201Created,
                await sender.Send(command, cancellationToken)));
    }

    [HttpPost("{classId:guid}/registrations")]
    [Authorize(Roles = "CUSTOMER")]
    [RequirePermission("class_registrations", "create")]
    [ProducesResponseType(typeof(RegisterForSwimClassResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RegisterForSwimClassResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(Guid classId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _)) return Forbid();
        return await ExecuteAsync(async () =>
        {
            var result = await sender.Send(new RegisterForSwimClassCommand(classId), cancellationToken);
            return result.ExistingOrder ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
        });
    }

    [HttpPut("{classId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("swim_classes", "update")]
    [ProducesResponseType(typeof(UpdateSwimClassResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid classId,
        [FromBody] UpdateSwimClassCommand command,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _)) return Forbid();
        command.ClassId = classId;
        return await ExecuteAsync(async () => Ok(await sender.Send(command, cancellationToken)));
    }

    [HttpPost("{classId:guid}/sessions")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("class_sessions", "create")]
    [ProducesResponseType(typeof(CreateClassSessionsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSessions(
        Guid classId,
        [FromBody] CreateClassSessionsCommand command,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _)) return Forbid();
        command.ClassId = classId;
        return await ExecuteAsync(async () =>
            StatusCode(StatusCodes.Status201Created,
                await sender.Send(command, cancellationToken)));
    }

    [HttpPatch("{classId:guid}/sessions/{sessionId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("class_sessions", "update")]
    [ProducesResponseType(typeof(UpdateClassSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSession(
        Guid classId,
        Guid sessionId,
        [FromBody] UpdateClassSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _)) return Forbid();
        command.ClassId = classId;
        command.SessionId = sessionId;
        return await ExecuteAsync(async () => Ok(await sender.Send(command, cancellationToken)));
    }

    [HttpPost("{classId:guid}/activate")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("swim_classes", "update")]
    [ProducesResponseType(typeof(ActivateSwimClassResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(Guid classId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _)) return Forbid();
        return await ExecuteAsync(async () =>
            Ok(await sender.Send(new ActivateSwimClassCommand(classId), cancellationToken)));
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
                message = "Dữ liệu lớp không hợp lệ.",
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
        catch (ClassSessionConflictException exception)
        {
            return new ConflictObjectResult(new { message = exception.Message, conflicts = exception.Conflicts });
        }
        catch (SwimClassStateConflictException exception)
        {
            return new ConflictObjectResult(new { message = exception.Message });
        }
    }
}
