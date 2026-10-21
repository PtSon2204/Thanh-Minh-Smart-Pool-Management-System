using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;
using System.Security.Claims;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/maintenance-schedules")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class MaintenanceController(IMaintenanceOperations operations, ILogger<MaintenanceController> logger) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] MaintenanceListQuery query, CancellationToken ct) => Execute(async () => Ok(await operations.GetListAsync(query, ct)));

    [HttpGet("options")]
    public Task<IActionResult> Options(CancellationToken ct) => Execute(async () => Ok(await operations.GetOptionsAsync(ct)));

    [HttpGet("history")]
    public Task<IActionResult> History([FromQuery] MaintenanceListQuery query, CancellationToken ct) => Execute(async () => Ok(await operations.GetHistoryAsync(query, ct)));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] MaintenanceRequest request, CancellationToken ct) => Execute(async () =>
    {
        var row = await operations.CreateAsync(request, ct);
        return StatusCode(201, row);
    });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] MaintenanceRequest request, CancellationToken ct) => Execute(async () => Ok(await operations.UpdateAsync(id, request, ct)));

    [HttpPost("{id:guid}/{operation:regex(^(start|pause|resume|cancel)$)}")]
    public Task<IActionResult> ChangeState(Guid id, string operation, CancellationToken ct) => Execute(async () =>
    {
        await operations.ChangeStateAsync(id, operation, ct);
        return NoContent();
    });

    [HttpPost("{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, [FromBody] CompleteMaintenanceRequest request, CancellationToken ct) => Execute(async () =>
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
        await operations.CompleteAsync(id, request, actor, ct);
        return NoContent();
    });

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> operation)
    {
        try { return await operation(); }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = "Vui lòng kiểm tra thông tin lịch bảo trì.",
                errors = exception.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(y => y.ErrorMessage).ToArray()) });
        }
        catch (MaintenanceException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to process maintenance request.");
            return StatusCode(500, new { message = "Không thể xử lý lịch bảo trì. Vui lòng thử lại." });
        }
    }
}
