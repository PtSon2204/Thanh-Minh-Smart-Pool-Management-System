using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/staff-attendance")]
public sealed class StaffAttendanceQrController : ControllerBase
{
    private readonly ISender sender;
    private readonly IStaffAttendanceSessionStore sessions;

    public StaffAttendanceQrController(ISender sender, IStaffAttendanceSessionStore sessions)
    {
        this.sender = sender;
        this.sessions = sessions;
    }

    [HttpPost("qr-sessions")]
    [RequirePermission("attendance", "update")]
    public async Task<IActionResult> CreateSession([FromBody] StaffAttendanceQrSessionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var issuerId) || !StaffAttendanceQrToken.TryParseAction(request.Action, out var action))
            return Forbid();
        try
        {
            var issued = await sessions.CreateAsync(new StaffAttendanceQrIssueRequest(request.ShiftId, request.WorkDate, action), issuerId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, issued);
        }
        catch (StaffAttendanceQrForbiddenException)
        {
            return Forbid();
        }
        catch (StaffAttendanceQrConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("qr-sessions/{id:guid}/rotate")]
    [RequirePermission("attendance", "update")]
    public async Task<IActionResult> RotateSession(Guid id, [FromBody] StaffAttendanceQrRotateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var issuerId))
            return Forbid();
        try
        {
            return Ok(await sessions.RotateAsync(id, request.Generation, issuerId, cancellationToken));
        }
        catch (StaffAttendanceQrForbiddenException)
        {
            return Forbid();
        }
        catch (StaffAttendanceQrConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpDelete("qr-sessions/{id:guid}")]
    [RequirePermission("attendance", "update")]
    public async Task<IActionResult> StopSession(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var issuerId))
            return Forbid();
        try
        {
            await sessions.StopAsync(id, issuerId, cancellationToken);
            return NoContent();
        }
        catch (StaffAttendanceQrForbiddenException)
        {
            return Forbid();
        }
    }

    [HttpPost("preview")]
    [RequirePermission("attendance", "update")]
    public Task<IActionResult> Preview([FromBody] StaffAttendanceQrPayloadRequest request, CancellationToken cancellationToken) => ResolveAsync(request, cancellationToken, null);

    [HttpPost("scan")]
    [RequirePermission("attendance", "update")]
    public Task<IActionResult> Scan([FromBody] StaffAttendanceQrPayloadRequest request, CancellationToken cancellationToken) => ResolveAsync(request, cancellationToken, resolution =>
        sender.Send(new RecordAttendanceCommand
        {
            Id = resolution.AssignmentId,
            CheckIn = resolution.CheckIn,
            OwnerId = GetUserId(),
            Method = "QR",
            PerformedByUserId = GetUserId()
        }, cancellationToken));

    private async Task<IActionResult> ResolveAsync(StaffAttendanceQrPayloadRequest request, CancellationToken cancellationToken,
        Func<StaffAttendanceQrResolution, Task<RecordAttendanceResponse>>? attendance)
    {
        if (!TryGetEligibleEmployeeId(out var employeeId))
            return Forbid();
        var resolution = await sessions.ResolveAsync(request.QrPayload, employeeId, cancellationToken);
        if (resolution is null)
            return BadRequest(new { message = "Mã QR chấm công không hợp lệ hoặc đã hết hạn." });
        if (attendance is null)
            return Ok(new StaffAttendanceQrPreviewResponse("StaffAttendance", resolution.SessionId, resolution.ShiftId,
                resolution.WorkDate, resolution.Action, resolution.TokenExpiresAtUtc, resolution.SessionExpiresAtUtc));
        try
        {
            return Ok(await attendance(resolution));
        }
        catch (StaffForbiddenException)
        {
            return Forbid();
        }
        catch (StaffConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    private bool TryGetAdminId(out Guid userId)
    {
        userId = Guid.Empty;
        return User.HasRole(RoleEnum.ADMIN) && User.TryGetUserId(out userId);
    }

    private bool TryGetEligibleEmployeeId(out Guid userId)
    {
        userId = GetUserId();
        return userId != Guid.Empty && (User.HasRole(RoleEnum.SALES) || User.HasRole(RoleEnum.LIFEGUARD) || User.HasRole(RoleEnum.COACH));
    }

    private Guid GetUserId() => User.TryGetUserId(out var userId) ? userId : Guid.Empty;
}
