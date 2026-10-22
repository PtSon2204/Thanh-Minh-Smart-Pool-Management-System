using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageSafetyAlerts.Commands.ActivateSafetyAlert;
using SmartPool.Application.Features.ManageSafetyAlerts.Commands.ConfirmSafetyIncident;
using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/safety-alerts")]
[Authorize]
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
    [Authorize(Roles = "ADMIN,LIFEGUARD")]
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
                Level = request.Level,
                Description = request.Description,
                ImageIncidents = request.ImageIncidents,
                TriggeredByUserId = userId,
                TriggeredByName = userName,
            };

            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy danh sách lịch sử sự cố và cảnh báo cứu hộ.
    /// </summary>
    [HttpGet("incidents")]
    [Authorize(Roles = "ADMIN,LIFEGUARD")]
    [ProducesResponseType(typeof(GetSafetyIncidentsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidents(
        [FromQuery] string? search,
        [FromQuery] string? zone,
        [FromQuery] string? severityLevel,
        [FromQuery] string? status,
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
            FromDate = fromDate,
            ToDate = toDate,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xác nhận xử lý sự cố cứu hộ.
    /// </summary>
    [HttpPut("incidents/{id:guid}/confirm")]
    [Authorize(Roles = "ADMIN,LIFEGUARD")]
    [ProducesResponseType(typeof(ConfirmSafetyIncidentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmIncident(
        [FromRoute] Guid id,
        [FromBody] ConfirmIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.FindFirstValue(ClaimTypes.Name)
                      ?? User.FindFirstValue("username")
                      ?? "Cứu hộ viên";

        var command = new ConfirmSafetyIncidentCommand
        {
            IncidentId = id,
            Status = request.Status ?? "Resolved",
            ResolutionNote = request.ResolutionNote,
            ConfirmedByUserId = userId,
            ConfirmedByName = userName,
        };

        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }
}

/// <summary>Request body cho activate alert.</summary>
public sealed record ActivateAlertRequest(
    string Zone,
    string Level = "EMERGENCY",
    string? Description = null,
    string? ImageIncidents = null
);

/// <summary>Request body cho xác nhận xử lý sự cố.</summary>
public sealed record ConfirmIncidentRequest(
    string? Status = "Resolved",
    string? ResolutionNote = null
);
