using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/thresholds")]
[Authorize(Policy = AuthorizationPolicies.SafetyOperators)]
public sealed class ThresholdsController(ISender sender) : ControllerBase
{
    [HttpGet("safety-monitoring")]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await sender.Send(new GetThresholdsQuery(), ct));

    [HttpPut("safety-monitoring")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Put([FromBody] SaveThresholdsCommand command, CancellationToken ct)
    {
        try { return Ok(await sender.Send(command, ct)); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Errors.First().ErrorMessage }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
