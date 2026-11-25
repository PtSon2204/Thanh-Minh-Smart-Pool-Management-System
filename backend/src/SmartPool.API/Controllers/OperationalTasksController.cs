using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/v1/operational-tasks")]
[Authorize]
public sealed class OperationalTasksController(ISender sender, INotificationService? notificationService = null) : ControllerBase
{
    [HttpGet]
    [RequirePermission("staff_tasks", "read")]
    public async Task<ActionResult<PagedResponse<PoolDutyScheduleDto>>> GetTasks(
        [FromQuery] GetOperationalTasksQuery query, CancellationToken cancellationToken)
    {
        var isAdmin = User.HasRole(RoleEnum.ADMIN);
        var isLifeguard = User.HasRole(RoleEnum.LIFEGUARD);
        if (!isAdmin && !isLifeguard) return Forbid();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        if (!isAdmin) query.EmployeeId = actorId;
        return Ok(await sender.Send(query, cancellationToken));
    }

    [HttpGet("{id:guid}/assignees")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("staff_tasks", "read")]
    public async Task<ActionResult<IReadOnlyList<PoolDutyEligibleEmployeeDto>>> GetAssignees(
        Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetOperationalTaskAssigneesQuery(id), cancellationToken));

    [HttpGet("{id:guid}/checklist-result")]
    [RequirePermission("staff_tasks", "read")]
    public async Task<IActionResult> GetChecklistResult(Guid id, CancellationToken cancellationToken)
    {
        var isAdmin = User.HasRole(RoleEnum.ADMIN);
        var isLifeguard = User.HasRole(RoleEnum.LIFEGUARD);
        if (!isAdmin && !isLifeguard) return Forbid();
        if (!TryGetActorId(out var actorId)) return Unauthorized();

        var query = new GetOperationalTaskChecklistResultQuery(id)
        {
            EmployeeId = isAdmin ? null : actorId
        };
        try
        {
            var result = await sender.Send(query, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("measurement-recurrences")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("staff_tasks", "read")]
    public async Task<ActionResult<IReadOnlyList<PoolDutyMeasurementRecurrenceDto>>> GetRecurrences(
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMeasurementRecurrencesQuery(), cancellationToken));

    [HttpPost("measurement-recurrences")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("staff_tasks", "create")]
    public IActionResult CreateRecurrence() =>
        Conflict(new { message = "Lịch đo định kỳ vô hạn đã dừng. Hãy dùng chức năng chuẩn bị tuần." });

    [HttpDelete("measurement-recurrences/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("staff_tasks", "update")]
    public async Task<IActionResult> StopRecurrence(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        try
        {
            var stopped = await sender.Send(new StopMeasurementRecurrenceCommand(id) { ActorId = actorId }, cancellationToken);
            return Ok(new { stopped });
        }
        catch (StaffConflictException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("checklists/ensure")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("staff_tasks", "create")]
    public async Task<IActionResult> EnsureChecklists([FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            var createdCount = await sender.Send(new EnsureChecklistTasksCommand(date), cancellationToken);
            return Ok(new { createdCount });
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("weeks/ensure")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("staff_tasks", "create")]
    public async Task<IActionResult> EnsureWeek([FromBody] EnsureOperationalTaskWeekCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        command.ActorId = actorId;
        try { return Ok(await sender.Send(command, cancellationToken)); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (StaffConflictException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpGet("weeks/configuration")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("staff_tasks", "read")]
    public async Task<ActionResult<OperationalTaskWeekConfigurationDto>> GetWeekConfiguration(
        [FromQuery] DateOnly weekStart, CancellationToken cancellationToken)
    {
        var configuration = await sender.Send(
            new GetOperationalTaskWeekConfigurationQuery(weekStart), cancellationToken);
        if (configuration is null) return NoContent();
        return Ok(configuration);
    }

    [HttpPut("{id:guid}/assignment")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("staff_tasks", "update")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignOperationalTaskCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.TaskId) return BadRequest(new { message = "Task ID does not match the route." });
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        command.ActorId = actorId;
        try
        {
            var result = await sender.Send(command, cancellationToken);
            await BroadcastWaterQualityGateSyncAsync(cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequirePermission("staff_tasks", "update")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOperationalTaskCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.TaskId) return BadRequest(new { message = "Task ID does not match the route." });
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        command.ActorId = actorId;
        try
        {
            var result = await sender.Send(command, cancellationToken);
            await BroadcastWaterQualityGateSyncAsync(cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    private async Task BroadcastWaterQualityGateSyncAsync(CancellationToken cancellationToken)
    {
        if (notificationService is null) return;
        try
        {
            await notificationService.SendToAllAsync("WaterQualityUpdated", new { source = "operational-task" }, cancellationToken);
        }
        catch
        {
            // Non-fatal: task mutation already succeeded
        }
    }

    private bool TryGetActorId(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
}
