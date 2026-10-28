using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.CancelMaintenance;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.CompleteMaintenance;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.CreateMaintenanceSchedule;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.PauseMaintenance;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.ResumeMaintenance;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.StartMaintenance;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.UpdateMaintenanceSchedule;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceHistory;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceOptions;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceSchedules;
using System.Security.Claims;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/maintenance-schedules")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class MaintenanceController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(ISender sender, ILogger<MaintenanceController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet]
    public Task<IActionResult> List([FromQuery] GetMaintenanceSchedulesQuery query, CancellationToken cancellationToken)
        => SendAsync(query, cancellationToken);

    [HttpGet("options")]
    public Task<IActionResult> Options(CancellationToken cancellationToken)
        => SendAsync(new GetMaintenanceOptionsQuery(), cancellationToken);

    [HttpGet("history")]
    public Task<IActionResult> History([FromQuery] GetMaintenanceHistoryQuery query, CancellationToken cancellationToken)
        => SendAsync(query, cancellationToken);

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateMaintenanceScheduleCommand command, CancellationToken cancellationToken)
        => SendAsync(command, cancellationToken, StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateMaintenanceScheduleCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        return SendAsync(command, cancellationToken);
    }

    [HttpPost("{id:guid}/start")]
    public Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
        => SendAsync(new StartMaintenanceCommand(id), cancellationToken);

    [HttpPost("{id:guid}/pause")]
    public Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken)
        => SendAsync(new PauseMaintenanceCommand(id), cancellationToken);

    [HttpPost("{id:guid}/resume")]
    public Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken)
        => SendAsync(new ResumeMaintenanceCommand(id), cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        => SendAsync(new CancelMaintenanceCommand(id), cancellationToken);

    [HttpPost("{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, [FromBody] CompleteMaintenanceCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor))
            return Task.FromResult<IActionResult>(Unauthorized());
        command.Id = id;
        command.PerformedBy = actor;
        return SendAsync(command, cancellationToken);
    }

    private async Task<IActionResult> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken,
        int successStatus = StatusCodes.Status200OK)
    {
        try { return StatusCode(successStatus, await _sender.Send(request, cancellationToken)); }
        catch (ValidationException exception)
        {
            return BadRequest(new
            {
                message = "Vui lòng kiểm tra thông tin lịch bảo trì.",
                errors = exception.Errors.GroupBy(x => x.PropertyName)
                    .ToDictionary(x => x.Key, x => x.Select(y => y.ErrorMessage).ToArray())
            });
        }
        catch (MaintenanceException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Unable to process maintenance request.");
            return StatusCode(500, new { message = "Không thể xử lý lịch bảo trì. Vui lòng thử lại." });
        }
    }
}
