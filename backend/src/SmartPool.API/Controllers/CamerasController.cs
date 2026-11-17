using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.ManageEquipments.Queries.GetCameraList;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/cameras")]
// Giám sát an toàn — ADMIN và LIFEGUARD được xem danh sách camera cùng dữ liệu phân tích AI.
[Authorize(Policy = AuthorizationPolicies.SafetyOperators)]
public sealed class CamerasController(ISender sender, SmartPool.Infrastructure.SafetyMonitoring.CameraDiagnostics diagnostics) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetCameraListQuery query, CancellationToken ct)
    {
        try { return Ok(await sender.Send(query, ct)); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Errors.First().ErrorMessage }); }
    }

    [HttpGet("areas")]
    public async Task<IActionResult> Areas(CancellationToken ct) => Ok(await sender.Send(new GetCameraAreasQuery(), ct));

    [HttpGet("{id:guid}/analysis")]
    public async Task<IActionResult> Analysis(Guid id, CancellationToken ct) => Ok(await sender.Send(new GetCameraAnalysisQuery(id), ct));

    [HttpGet("{id:guid}/analysis/frame")]
    public async Task<IActionResult> AnalysisFrame(Guid id, [FromQuery] string frameId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var jpeg = await sender.Send(new GetCameraAnalysisFrameQuery(id, frameId), ct);
        return jpeg == null ? StatusCode(410) : File(jpeg, "image/jpeg");
    }

    // Optional, bounded pipeline diagnostics. Returns [] unless CameraSettings:
    // Diagnostics:Enabled is on; the ring buffer only records while enabled, so
    // this endpoint never exposes frames, URLs, credentials or PII.
    [HttpGet("{id:guid}/diagnostics")]
    public IActionResult Diagnostics(Guid id, [FromQuery] int limit = 512)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(diagnostics.Get(id, limit));
    }
}
