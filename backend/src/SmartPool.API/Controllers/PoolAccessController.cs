using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.AccessControlPool.Commands;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Features.AccessControlPool.Queries;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/v1/pool-access")]
public sealed class PoolAccessController(ISender sender) : ControllerBase
{
    [HttpPost("lookup")]
    public async Task<ActionResult<TicketLookupResult>> Lookup([FromBody] LookupTicketRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LookupTicketQuery(request.Code), cancellationToken);
        return result.IsValid ? Ok(result.Value) : BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>(result.Errors!)));
    }

    [HttpPost("entries")]
    public async Task<ActionResult<EntryConfirmationResult>> Confirm([FromBody] ConfirmEntryRequest request, CancellationToken cancellationToken)
    {
        var operatorId = GetOperatorId();
        if (operatorId is null)
            return Unauthorized();

        var result = await sender.Send(new ConfirmEntryCommand(request.Code, request.InputMode, operatorId.Value), cancellationToken);
        return result.IsValid ? Ok(result.Value) : BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>(result.Errors!)));
    }

    [HttpGet("entries")]
    public async Task<ActionResult<PagedResult<EntryHistoryItemDto>>> GetHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] DateOnly? date = null,
        [FromQuery] string? status = null, [FromQuery] Guid? ticketId = null, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetEntryHistoryQuery(page, pageSize, date, status, ticketId), cancellationToken);
        return result.IsValid ? Ok(result.Value) : BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>(result.Errors!)));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DailyEntrySummaryDto>> GetSummary([FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDailyEntrySummaryQuery(date), cancellationToken);
        return result.IsValid ? Ok(result.Value) : BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>(result.Errors!)));
    }

    private Guid? GetOperatorId() => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var operatorId)
        ? operatorId
        : null;
}

public sealed record LookupTicketRequest(string? Code);

public sealed record ConfirmEntryRequest(string? Code, string? InputMode);
