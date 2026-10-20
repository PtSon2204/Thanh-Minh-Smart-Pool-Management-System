using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Features.GoodsReceipts;
using SmartPool.Application.Features.ManageServices;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/goods-receipts")]
public sealed class GoodsReceiptsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission("inventory", "read")]
    public Task<IActionResult> GetAll([FromQuery] GetGoodsReceiptsQuery query, CancellationToken cancellationToken) => SendAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequirePermission("inventory", "read")]
    public Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) => SendAsync(new GetGoodsReceiptQuery(id), cancellationToken);

    [HttpPost]
    [RequirePermission("inventory", "update")]
    public async Task<IActionResult> Create([FromBody] CreateGoodsReceiptCommand command, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Forbid();
        command.OperatorId = userId;
        return await SendAsync(command, cancellationToken, StatusCodes.Status201Created);
    }

    [HttpPost("{id:guid}/post")]
    [RequirePermission("inventory", "update")]
    public Task<IActionResult> Post(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Task.FromResult<IActionResult>(Forbid());
        return SendAsync(new PostGoodsReceiptCommand(id, userId), cancellationToken);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("inventory", "update")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) => SendAsync(new CancelGoodsReceiptCommand(id), cancellationToken);

    private async Task<IActionResult> SendAsync<T>(IRequest<T> request, CancellationToken cancellationToken, int status = StatusCodes.Status200OK)
    {
        try { return StatusCode(status, await sender.Send(request, cancellationToken)); }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = "Dữ liệu phiếu nhập không hợp lệ.", errors = exception.Errors
                .GroupBy(error => error.PropertyName).ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()) });
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ServiceConflictException exception) { return Conflict(new { message = exception.Message }); }
    }
}
