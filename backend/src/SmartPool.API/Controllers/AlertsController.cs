using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    [HttpGet("filters")]
    public Task<IActionResult> Filters(CancellationToken ct) => Send(new GetAlertFiltersQuery(), ct);
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => Send(new GetAlertQuery(id), ct);
    [HttpPatch("{id:guid}/acknowledge")]
    public Task<IActionResult> Acknowledge(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AcknowledgeAlertRequest? request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.ExpectedSeverity))
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Vui lòng tải lại cảnh báo trước khi xác nhận." }));
        return Send(new HandleAlertCommand(id, "ACKNOWLEDGED", request.ExpectedSeverity), ct);
    }
    [HttpPatch("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AcknowledgeAlertRequest? request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.ExpectedSeverity))
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Vui lòng tải lại cảnh báo trước khi hủy." }));
        return Send(new HandleAlertCommand(id, "CANCELLED", request.ExpectedSeverity), ct);
    }
    private async Task<IActionResult> Send<T>(IRequest<T> request, CancellationToken ct)
    {
        try { return Ok(await sender.Send(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (MonitoringConflictException ex) { return Conflict(new { message = ex.Message, alert = ex.CurrentAlert }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

public sealed record AcknowledgeAlertRequest(string ExpectedSeverity);
