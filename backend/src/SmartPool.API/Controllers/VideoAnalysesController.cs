using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/video-analyses")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class VideoAnalysesController(ISender sender) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await sender.Send(new GetMonitoringStatusQuery(), ct));

    [HttpPost]
    [RequestSizeLimit(210 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 210 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var job = await sender.Send(new UploadVideoCommand(stream, file.FileName, file.Length), ct);
            return AcceptedAtAction(nameof(Get), new { jobId = job.Id }, job);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(503, new { message = ex.Message }); }
        catch (MonitoringConflictException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("{jobId:guid}")]
    [HttpGet("{jobId:guid}/result")]
    public async Task<IActionResult> Get(Guid jobId, CancellationToken ct)
    {
        try { return Ok(await sender.Send(new GetVideoJobQuery(jobId), ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpGet("{jobId:guid}/alerts")]
    public async Task<IActionResult> Alerts(Guid jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            await sender.Send(new GetVideoJobQuery(jobId), ct);
            return Ok(await sender.Send(new GetAlertsQuery("UPLOAD", JobId: jobId, Page: page, PageSize: pageSize), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpGet("{jobId:guid}/video")]
    public async Task<IActionResult> Video(Guid jobId, CancellationToken ct)
    {
        try { return PhysicalFile(await sender.Send(new GetVideoFileQuery(jobId), ct), "video/mp4", enableRangeProcessing: true); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (MonitoringConflictException ex) { return Conflict(new { message = ex.Message }); }
    }
    [HttpGet("{jobId:guid}/playback")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Playback(Guid jobId, CancellationToken ct, [FromQuery] bool original = false)
    {
        try { return Ok(await sender.Send(new GetVideoPlaybackQuery(jobId, original), ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (MonitoringConflictException ex) { return Conflict(new { message = ex.Message }); }
    }
    [AllowAnonymous]
    [HttpGet("{jobId:guid}/stream")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Stream(Guid jobId, [FromQuery] string ticket = "", [FromQuery] bool download = false, CancellationToken ct = default, [FromQuery] bool original = false)
    {
        try
        {
            var path = await sender.Send(new GetVideoStreamQuery(jobId, ticket, original), ct);
            return PhysicalFile(path, "video/mp4", fileDownloadName: download ? $"analysis-{jobId}.mp4" : null, enableRangeProcessing: true);
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (MonitoringConflictException ex) { return Conflict(new { message = ex.Message }); }
    }
}
