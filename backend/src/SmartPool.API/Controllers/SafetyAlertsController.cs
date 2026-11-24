using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageSafetyAlerts.Commands.ActivateSafetyAlert;
using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidentById;
using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;
using SmartPool.API.Authorization;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/safety-alerts")]
[Authorize(Policy = AuthorizationPolicies.SafetyOperators)]
public sealed class SafetyAlertsController : ControllerBase
{
    private readonly ISender _sender;

    public SafetyAlertsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Kích hoạt cảnh báo cứu hộ thủ công.
    /// Broadcast SignalR event "safety_alert" đến tất cả thiết bị đang kết nối.
    /// Chỉ ADMIN và LIFEGUARD mới có quyền.
    /// </summary>
    [HttpPost("activate")]
    [ProducesResponseType(typeof(ActivateSafetyAlertResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateAlert(
        [FromBody] ActivateAlertRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var userName = User.FindFirstValue(ClaimTypes.Name)
                          ?? User.FindFirstValue("username")
                          ?? "Nhân viên";

            var command = new ActivateSafetyAlertCommand
            {
                Zone = request.Zone,
                ClientRequestId = request.ClientRequestId,
                Level = request.Level,
                Description = request.Description,
                ImageIncidents = request.ImageIncidents,
                TriggeredByUserId = userId,
                TriggeredByName = userName,
            };

            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy danh sách lịch sử sự cố và cảnh báo cứu hộ.
    /// </summary>
    [HttpGet("incidents")]
    [ProducesResponseType(typeof(GetSafetyIncidentsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidents(
        [FromQuery] string? search,
        [FromQuery] string? zone,
        [FromQuery] string? severityLevel,
        [FromQuery] string? status,
        [FromQuery] string? sourceType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSafetyIncidentsQuery
        {
            Search = search,
            Zone = zone,
            SeverityLevel = severityLevel,
            Status = status,
            SourceType = sourceType,
            FromDate = fromDate,
            ToDate = toDate,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        try { return Ok(await _sender.Send(query, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Lấy chi tiết một sự cố trong lịch sử.
    /// </summary>
    [HttpGet("incidents/{id:guid}")]
    [ProducesResponseType(typeof(SafetyIncidentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIncidentById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await _sender.Send(new GetSafetyIncidentByIdQuery(id), cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(new { message = "Không tìm thấy sự cố." }); }
    }
}

/// <summary>Request body cho activate alert.</summary>
public sealed record ActivateAlertRequest(
    string Zone,
    string Level = "EMERGENCY",
    string? Description = null,
    string? ImageIncidents = null,
    Guid? ClientRequestId = null
);
