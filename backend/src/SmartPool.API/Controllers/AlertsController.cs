using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.API.Authorization;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize(Policy = AuthorizationPolicies.SafetyOperators)]
public sealed class AlertsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] GetAlertsQuery query, CancellationToken ct) => Send(query, ct);
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => Send(new GetAlertQuery(id), ct);
    [HttpPatch("{id:guid}/acknowledge")]
    public Task<IActionResult> Acknowledge(Guid id, CancellationToken ct) => Send(new HandleAlertCommand(id, "ACKNOWLEDGED"), ct);
    [HttpPatch("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) => Send(new HandleAlertCommand(id, "CANCELLED"), ct);
    private async Task<IActionResult> Send<T>(IRequest<T> request, CancellationToken ct)
    {
        try { return Ok(await sender.Send(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (MonitoringConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
