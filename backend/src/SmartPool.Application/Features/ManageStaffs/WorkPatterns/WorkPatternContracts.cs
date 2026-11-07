using System.Text.Json.Serialization;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.WorkPatterns;

public sealed record WeeklyPatternDay(int DayOfWeek, IReadOnlyList<Guid> ShiftIds);
public sealed record ContractShiftView(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime, bool IsActive);
public sealed record ContractTerms(string EmploymentType, DateOnly StartDate, DateOnly EndDate,
    IReadOnlyList<WeeklyPatternDay> WeeklyPattern);
public sealed record ScheduleDetail(DateOnly WorkDate, Guid? ShiftId, string Code, string Message);
public sealed record AffectedScheduleTask(Guid Id, DateOnly WorkDate, Guid? PreviousAssigneeId, string Status);
public sealed record ScheduleResult(string? PreviewToken, int CreatedCount, int CancelledCount, int SkippedCount,
    int ConflictCount, IReadOnlyList<ScheduleDetail> Details, IReadOnlyList<AffectedScheduleTask> AffectedTasks,
    string Status, Guid? OperationId = null);
public sealed record WorkPatternResponse(ContractTerms? Contract, long Version, ScheduleResult Result);
public sealed record WorkPatternRevisionView(Guid Id, ContractTerms Contract, DateOnly EffectiveFrom,
    long Version, Guid CreatedBy, DateTime CreatedAtUtc);
public sealed record ScheduleExceptionView(Guid Id, string Kind, DateOnly WorkDate,
    IReadOnlyList<Guid> SourceAssignmentIds, Guid? DestinationShiftId, string Reason, long Version,
    Guid? DestinationEmployeeId = null, string? DestinationEmployeeName = null);
public sealed record ScheduleDeliveryView(Guid Id, Guid OperationId, string Channel, string Status,
    int AttemptCount, DateTime CreatedAtUtc, string? LastErrorCode);
public sealed record WorkPatternView(ContractTerms? Contract, long Version,
    IReadOnlyList<WorkPatternRevisionView> History, IReadOnlyList<ScheduleExceptionView> Exceptions,
    decimal ExpectedWeeklyHours, IReadOnlyList<ScheduleDeliveryView> Deliveries,
    IReadOnlyList<ContractShiftView>? Shifts = null);

public enum WorkPatternAction { Get, Preview, Confirm, ExceptionPreview, ExceptionConfirm, Receipt }

public sealed class WorkPatternCommand : IRequest<object>
{
    [JsonIgnore] public WorkPatternAction Action { get; set; }
    [JsonIgnore] public Guid EmployeeId { get; set; }
    [JsonIgnore] public Guid ActorId { get; set; }
    public ContractTerms? Contract { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public long ExpectedVersion { get; set; }
    public string? PreviewToken { get; set; }
    public Guid OperationId { get; set; }
    public string? Kind { get; set; }
    public DateOnly WorkDate { get; set; }
    public IReadOnlyList<Guid> SourceAssignmentIds { get; set; } = [];
    public Guid? DestinationShiftId { get; set; }
    public string? Reason { get; set; }
}

public sealed class WorkPatternHandler(IStaffOperations operations) : IRequestHandler<WorkPatternCommand, object>
{
    public Task<object> Handle(WorkPatternCommand request, CancellationToken cancellationToken) =>
        operations.ExecuteWorkPatternAsync(request, cancellationToken);
}
