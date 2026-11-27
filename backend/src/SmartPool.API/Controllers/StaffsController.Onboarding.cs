using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;

namespace SmartPool.API.Controllers;

public sealed partial class StaffsController
{
    [HttpGet("{employeeId:guid}/work-pattern"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "read")]
    public Task<IActionResult> GetWorkPattern(Guid employeeId, CancellationToken cancellationToken) =>
        WorkPatternAsync(new WorkPatternCommand { EmployeeId = employeeId, Action = WorkPatternAction.Get }, cancellationToken);

    [HttpPost("{employeeId:guid}/work-pattern/preview"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "create")]
    public Task<IActionResult> PreviewWorkPattern(Guid employeeId, [FromBody] WorkPatternCommand command, CancellationToken cancellationToken)
    { command.EmployeeId = employeeId; command.Action = WorkPatternAction.Preview; return WorkPatternAsync(command, cancellationToken); }

    [HttpPost("{employeeId:guid}/work-pattern/confirm"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "create")]
    public Task<IActionResult> ConfirmWorkPattern(Guid employeeId, [FromBody] WorkPatternCommand command, CancellationToken cancellationToken)
    { command.EmployeeId = employeeId; command.Action = WorkPatternAction.Confirm; return WorkPatternAsync(command, cancellationToken); }

    [HttpPost("{employeeId:guid}/work-pattern/renewal/preview"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "create")]
    public Task<IActionResult> PreviewContractRenewal(Guid employeeId, [FromBody] WorkPatternCommand command, CancellationToken cancellationToken)
    { command.EmployeeId = employeeId; command.Action = WorkPatternAction.RenewalPreview; return WorkPatternAsync(command, cancellationToken); }

    [HttpPost("{employeeId:guid}/work-pattern/renewal/confirm"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "create")]
    public Task<IActionResult> ConfirmContractRenewal(Guid employeeId, [FromBody] WorkPatternCommand command, CancellationToken cancellationToken)
    { command.EmployeeId = employeeId; command.Action = WorkPatternAction.RenewalConfirm; return WorkPatternAsync(command, cancellationToken); }

    [HttpPost("{employeeId:guid}/work-pattern/exceptions/preview"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "create")]
    public Task<IActionResult> PreviewScheduleException(Guid employeeId, [FromBody] WorkPatternCommand command, CancellationToken cancellationToken)
    { command.EmployeeId = employeeId; command.Action = WorkPatternAction.ExceptionPreview; return WorkPatternAsync(command, cancellationToken); }

    [HttpPost("{employeeId:guid}/work-pattern/exceptions/confirm"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "create")]
    public Task<IActionResult> ConfirmScheduleException(Guid employeeId, [FromBody] WorkPatternCommand command, CancellationToken cancellationToken)
    { command.EmployeeId = employeeId; command.Action = WorkPatternAction.ExceptionConfirm; return WorkPatternAsync(command, cancellationToken); }

    [HttpGet("{employeeId:guid}/work-pattern/operations/{operationId:guid}"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "read")]
    public Task<IActionResult> ScheduleOperation(Guid employeeId, Guid operationId, CancellationToken cancellationToken) =>
        WorkPatternAsync(new WorkPatternCommand { EmployeeId = employeeId, OperationId = operationId, Action = WorkPatternAction.Receipt }, cancellationToken);

    private Task<IActionResult> WorkPatternAsync(WorkPatternCommand command, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actor)) return Task.FromResult<IActionResult>(Unauthorized());
        command.ActorId = actor;
        Response.Headers.CacheControl = "no-store";
        return SendAsync(command, cancellationToken);
    }

    [HttpPost("schedule/assignments/{assignmentId:guid}/replacement/preview"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "create")]
    public Task<IActionResult> PreviewStaffShiftReplacement(Guid assignmentId, [FromBody] ReplaceStaffShiftCommand command, CancellationToken cancellationToken) =>
        StaffShiftReplacementAsync(assignmentId, command, StaffShiftReplacementAction.Preview, cancellationToken);

    [HttpPost("schedule/assignments/{assignmentId:guid}/replacement/confirm"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "create")]
    public Task<IActionResult> ConfirmStaffShiftReplacement(Guid assignmentId, [FromBody] ReplaceStaffShiftCommand command, CancellationToken cancellationToken) =>
        StaffShiftReplacementAsync(assignmentId, command, StaffShiftReplacementAction.Confirm, cancellationToken);

    [HttpGet("schedule/replacements/operations/{operationId:guid}"), Authorize(Policy = AuthorizationPolicies.AdminOnly), RequirePermission("staff_schedule", "read")]
    public Task<IActionResult> StaffShiftReplacementOperation(Guid operationId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actor)) return Task.FromResult<IActionResult>(Unauthorized());
        Response.Headers.CacheControl = "no-store";
        return SendAsync(new ReplaceStaffShiftCommand { Action = StaffShiftReplacementAction.Receipt, ActorId = actor, OperationId = operationId }, cancellationToken);
    }

    private Task<IActionResult> StaffShiftReplacementAsync(Guid assignmentId, ReplaceStaffShiftCommand command,
        StaffShiftReplacementAction action, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actor)) return Task.FromResult<IActionResult>(Unauthorized());
        command.SourceAssignmentId = assignmentId;
        command.Action = action;
        command.ActorId = actor;
        Response.Headers.CacheControl = "no-store";
        return SendAsync(command, cancellationToken);
    }
}
