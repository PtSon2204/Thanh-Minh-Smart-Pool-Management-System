using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageStaffs.Commands;
using SmartPool.Application.Features.ManageStaffs.Contracts;
using SmartPool.Application.Features.ManageStaffs.Queries;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/v1/shifts")]
public sealed class ShiftsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetShifts([FromQuery] ShiftListRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new GetShiftsQuery(request), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateShift(SaveShiftRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new SaveShiftCommand(null, request), cancellationToken), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateShift(Guid id, SaveShiftRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new SaveShiftCommand(id, request), cancellationToken));

    private IActionResult ToAction<T>(OperationResult<T> result, int successStatus = StatusCodes.Status200OK) => result.Error switch
    {
        null => StatusCode(successStatus, result.Value),
        StaffError.Validation => ValidationProblem(detail: result.Message),
        StaffError.NotFound => NotFound(new ProblemDetails { Detail = result.Message }),
        _ => Conflict(new ProblemDetails { Detail = result.Message })
    };
}
