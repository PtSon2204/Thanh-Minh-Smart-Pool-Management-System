using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/video-analyses")]
[Authorize(Policy = AuthorizationPolicies.SafetyOperators)]
public sealed class VideoFrameAnalysisController(ISender sender) : ControllerBase
{
    [HttpGet("{jobId:guid}/frame-analysis/window")]
    public async Task<IActionResult> Window(Guid jobId, [FromQuery] double from = 0, [FromQuery] double duration = 4, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await sender.Send(new GetVideoFrameAnalysisWindowQuery(jobId, from, duration), ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    // Decisions (box cảnh báo) của frame gần nhất tại `seconds` trong lúc job đang phân tích —
    // cho video phát song song overlay đúng như video kết quả đã vẽ khung.
    [HttpGet("{jobId:guid}/frame-analysis")]
    public async Task<IActionResult> FrameAnalysis(Guid jobId, [FromQuery] double seconds, CancellationToken ct)
    {
        try { return Ok(await sender.Send(new GetVideoFrameAnalysisQuery(jobId, seconds), ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return StatusCode(403, new { message = "Không có quyền xem lần phân tích này." }); }
    }
}
