using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageServices.Commands;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Features.ManageServices.Queries;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/v1/services")]
public sealed class ServicesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageDto<ServiceDto>>> GetServices([FromQuery] ServiceListQuery query, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new GetServicesQuery(query), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ServiceDto>> CreateService([FromBody] CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateServiceCommand(request), cancellationToken);
        return result.IsSuccess ? Created($"/api/v1/services/{result.Value!.Id}", result.Value) : ToActionResult(result);
    }

    [HttpPut("{serviceId:guid}")]
    public async Task<ActionResult<ServiceDto>> UpdateService(Guid serviceId, [FromBody] UpdateServiceRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new UpdateServiceCommand(serviceId, request), cancellationToken));

    [HttpPost("{serviceId:guid}/stock-adjustments")]
    public async Task<ActionResult<StockAdjustmentDto>> AdjustStock(Guid serviceId, [FromBody] AdjustStockRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetOperatorId(out var operatorId)) return Forbid();
        return ToActionResult(await sender.Send(new AdjustStockCommand(serviceId, request, operatorId), cancellationToken));
    }

    [HttpGet("{serviceId:guid}/inventory-history")]
    public async Task<ActionResult<PageDto<InventoryLogDto>>> GetInventoryHistory(Guid serviceId, [FromQuery] InventoryHistoryQuery query, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new GetInventoryHistoryQuery(serviceId, query), cancellationToken));

    private bool TryGetOperatorId(out Guid operatorId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out operatorId);

    private ActionResult<T> ToActionResult<T>(ServiceOperationResult<T> result) => result.Error switch
    {
        null => Ok(result.Value),
        ServiceOperationError.Validation => BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>(result.Errors))),
        ServiceOperationError.NotFound => NotFound(result.Errors),
        ServiceOperationError.Conflict => Conflict(result.Errors),
        _ => Problem()
    };
}
