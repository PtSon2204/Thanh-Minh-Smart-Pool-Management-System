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
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class CamerasController(ISender sender) : ControllerBase
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
}
