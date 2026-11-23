using System.Text.Json.Serialization;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageStaffs.WorkPatterns;

public enum StaffShiftReplacementAction { Preview, Confirm, Receipt }

public sealed class ReplaceStaffShiftCommand : IRequest<StaffShiftReplacementResult>
{
    [JsonIgnore] public StaffShiftReplacementAction Action { get; set; }
    [JsonIgnore] public Guid SourceAssignmentId { get; set; }
    [JsonIgnore] public Guid ActorId { get; set; }
    public Guid ReplacementEmployeeId { get; set; }
    public string Reason { get; set; } = "";
    public string? PreviewToken { get; set; }
    public Guid OperationId { get; set; }
}

public sealed record PoolDutyTransferView(Guid Id, DateOnly WorkDate, string Status);

public sealed record StaffShiftReplacementResult(
    Guid SourceAssignmentId,
    Guid OriginalEmployeeId,
    string OriginalEmployeeName,
    Guid ReplacementEmployeeId,
    string ReplacementEmployeeName,
    DateOnly WorkDate,
    Guid ShiftId,
    string ShiftName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool ReusesExistingAssignment,
    Guid? ReplacementAssignmentId,
    IReadOnlyList<PoolDutyTransferView> Duties,
    string Status,
    string? ConflictCode,
    string? Message,
    string? PreviewToken = null,
    Guid? OperationId = null);

public sealed class ReplaceStaffShiftHandler(
    IStaffOperations operations,
    INotificationService? notificationService = null) : IRequestHandler<ReplaceStaffShiftCommand, StaffShiftReplacementResult>
{
    public async Task<StaffShiftReplacementResult> Handle(ReplaceStaffShiftCommand request, CancellationToken cancellationToken)
    {
        var result = await operations.ReplaceStaffShiftAsync(request, cancellationToken);
        if (request.Action == StaffShiftReplacementAction.Confirm)
        {
            await StaffScheduleBroadcast.NotifyWaterQualityDutyChangedAsync(notificationService, "SHIFT_REPLACED", cancellationToken);
        }
        return result;
    }
}
