using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageStaffs.Commands;
using SmartPool.Application.Features.ManageStaffs.Contracts;
using SmartPool.Application.Features.ManageStaffs.Queries;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/v1/staffs")]
public sealed class StaffsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStaffs([FromQuery] StaffListRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new GetStaffsQuery(request), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateStaff(CreateStaffRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new CreateStaffCommand(request), cancellationToken), StatusCodes.Status201Created);

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken) => ToAction(await sender.Send(new GetStaffOptionsQuery(), cancellationToken));

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> UpdateStaff(Guid userId, UpdateStaffRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new UpdateStaffCommand(userId, request), cancellationToken));

    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule([FromQuery] ScheduleListRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new GetScheduleQuery(request), cancellationToken));

    [HttpPost("schedule")]
    public async Task<IActionResult> AssignShift(AssignShiftRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new AssignShiftCommand(request), cancellationToken), StatusCodes.Status201Created);

    [HttpDelete("schedule/{id:guid}")]
    public async Task<IActionResult> CancelShift(Guid id, CancellationToken cancellationToken) => ToAction(await sender.Send(new CancelShiftAssignmentCommand(id), cancellationToken));

    [HttpPost("schedule/{id:guid}/check-in")]
    public Task<IActionResult> CheckIn(Guid id, CancellationToken cancellationToken) => Attend(id, true, cancellationToken);

    [HttpPost("schedule/{id:guid}/check-out")]
    public Task<IActionResult> CheckOut(Guid id, CancellationToken cancellationToken) => Attend(id, false, cancellationToken);

    [HttpGet("me/schedule")]
    public async Task<IActionResult> GetMySchedule([FromQuery] PageRequest request, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        return ToAction(await sender.Send(new GetScheduleQuery(new(request.Page, request.PageSize, userId, date, null, true)), cancellationToken));
    }

    [HttpGet("salaries")]
    public async Task<IActionResult> GetSalaries([FromQuery] SalaryListRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new GetSalariesQuery(request), cancellationToken));

    [HttpPost("{userId:guid}/salaries")]
    public async Task<IActionResult> CreateSalary(Guid userId, SaveSalaryRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new SaveSalaryCommand(userId, null, request), cancellationToken), StatusCodes.Status201Created);

    [HttpPut("{userId:guid}/salaries/{id:guid}")]
    public async Task<IActionResult> UpdateSalary(Guid userId, Guid id, SaveSalaryRequest request, CancellationToken cancellationToken) => ToAction(await sender.Send(new SaveSalaryCommand(userId, id, request), cancellationToken));

    private async Task<IActionResult> Attend(Guid id, bool checkIn, CancellationToken cancellationToken)
    {
        Guid? ownerId = null;
        if (!User.IsInRole("Admin"))
        {
            if (!TryGetUserId(out var userId)) return Forbid();
            ownerId = userId;
        }
        return ToAction(await sender.Send(new RecordAttendanceCommand(id, checkIn, ownerId), cancellationToken));
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    private IActionResult ToAction<T>(OperationResult<T> result, int successStatus = StatusCodes.Status200OK) => result.Error switch
    {
        null => StatusCode(successStatus, result.Value),
        StaffError.Validation => ValidationProblem(detail: result.Message),
        StaffError.NotFound => NotFound(new ProblemDetails { Detail = result.Message }),
        StaffError.Forbidden => Forbid(),
        _ => Conflict(new ProblemDetails { Detail = result.Message })
    };
}
