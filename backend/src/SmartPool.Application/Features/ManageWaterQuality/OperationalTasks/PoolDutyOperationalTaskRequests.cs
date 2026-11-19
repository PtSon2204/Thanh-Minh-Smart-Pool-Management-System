using MediatR;
using System.Text.Json.Serialization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;

public sealed record GetOperationalTasksQuery(DateOnly? FromDate = null, DateOnly? ToDate = null,
    string? TaskType = null, string? Status = null, int PageIndex = 1, int PageSize = 50)
    : IRequest<PagedResponse<PoolDutyScheduleDto>>
{
    public Guid? EmployeeId { get; set; }
    public Guid? TaskId { get; set; }
}

public sealed record GetOperationalTaskAssigneesQuery(Guid TaskId)
    : IRequest<IReadOnlyList<PoolDutyEligibleEmployeeDto>>;

public sealed record GetOperationalTaskChecklistResultQuery(Guid TaskId) : IRequest<DailyInspectionDto?>
{
    [JsonIgnore] public Guid? EmployeeId { get; set; }
}

public sealed record GetMeasurementRecurrencesQuery : IRequest<IReadOnlyList<PoolDutyMeasurementRecurrenceDto>>;

public sealed record GetOperationalTaskWeekConfigurationQuery(DateOnly WeekStart)
    : IRequest<OperationalTaskWeekConfigurationDto?>;

public sealed record EnsureChecklistTasksCommand(DateOnly Date) : IRequest<int>;

public sealed record EnsureOperationalTaskWeekCommand(DateOnly WeekStart, int StartDayOfWeek, TimeOnly ScheduledTime,
    Guid ShiftId, Guid PreferredEmployeeId) : IRequest<EnsureOperationalTaskWeekResultDto>
{
    [JsonIgnore] public Guid ActorId { get; set; }
}

public sealed record EnsureOperationalTaskWeekResultDto(int CreatedCount, int ExistingCount, int UnassignedCount,
    OperationalTaskWeekConfigurationDto Configuration);

public sealed record OperationalTaskExecutionAuthorization(bool IsAdmin, bool IsLifeguard,
    bool AdminExecutionConfirmed, long? ExpectedTaskRevision, Guid? ExpectedAssigneeId);

public sealed record StopMeasurementRecurrenceCommand(Guid RecurrenceId) : IRequest<bool>
{
    [JsonIgnore] public Guid ActorId { get; set; }
}

public sealed record AssignOperationalTaskCommand(Guid TaskId, Guid EmployeeId, Guid EmployeeShiftId,
    long ExpectedRevision) : IRequest<PoolDutyScheduleDto>
{
    [JsonIgnore] public Guid ActorId { get; set; }
}

public sealed record CancelOperationalTaskCommand(Guid TaskId, long ExpectedRevision)
    : IRequest<PoolDutyScheduleDto>
{
    [JsonIgnore] public Guid ActorId { get; set; }
}

public sealed class GetOperationalTasksHandler(IWaterQualityOperations operations)
    : IRequestHandler<GetOperationalTasksQuery, PagedResponse<PoolDutyScheduleDto>>
{
    public Task<PagedResponse<PoolDutyScheduleDto>> Handle(GetOperationalTasksQuery request, CancellationToken cancellationToken) =>
        operations.GetOperationalTasksAsync(request, cancellationToken);
}

public sealed class GetOperationalTaskAssigneesHandler(IWaterQualityOperations operations)
    : IRequestHandler<GetOperationalTaskAssigneesQuery, IReadOnlyList<PoolDutyEligibleEmployeeDto>>
{
    public Task<IReadOnlyList<PoolDutyEligibleEmployeeDto>> Handle(GetOperationalTaskAssigneesQuery request, CancellationToken cancellationToken) =>
        operations.GetOperationalTaskAssigneesAsync(request.TaskId, cancellationToken);
}

public sealed class GetOperationalTaskChecklistResultHandler(IWaterQualityOperations operations)
    : IRequestHandler<GetOperationalTaskChecklistResultQuery, DailyInspectionDto?>
{
    public Task<DailyInspectionDto?> Handle(GetOperationalTaskChecklistResultQuery request,
        CancellationToken cancellationToken) =>
        operations.GetOperationalTaskChecklistResultAsync(request.TaskId, request.EmployeeId, cancellationToken);
}

public sealed class GetMeasurementRecurrencesHandler(IWaterQualityOperations operations)
    : IRequestHandler<GetMeasurementRecurrencesQuery, IReadOnlyList<PoolDutyMeasurementRecurrenceDto>>
{
    public Task<IReadOnlyList<PoolDutyMeasurementRecurrenceDto>> Handle(GetMeasurementRecurrencesQuery request, CancellationToken cancellationToken) =>
        operations.GetMeasurementRecurrencesAsync(cancellationToken);
}

public sealed class GetOperationalTaskWeekConfigurationHandler(IWaterQualityOperations operations)
    : IRequestHandler<GetOperationalTaskWeekConfigurationQuery, OperationalTaskWeekConfigurationDto?>
{
    public Task<OperationalTaskWeekConfigurationDto?> Handle(GetOperationalTaskWeekConfigurationQuery request,
        CancellationToken cancellationToken) =>
        operations.GetOperationalTaskWeekConfigurationAsync(request.WeekStart, cancellationToken);
}

public sealed class EnsureChecklistTasksHandler(IWaterQualityOperations operations)
    : IRequestHandler<EnsureChecklistTasksCommand, int>
{
    public Task<int> Handle(EnsureChecklistTasksCommand request, CancellationToken cancellationToken) =>
        operations.EnsureChecklistTasksAsync(request.Date, cancellationToken);
}

public sealed class EnsureOperationalTaskWeekHandler(IWaterQualityOperations operations)
    : IRequestHandler<EnsureOperationalTaskWeekCommand, EnsureOperationalTaskWeekResultDto>
{
    public Task<EnsureOperationalTaskWeekResultDto> Handle(EnsureOperationalTaskWeekCommand request, CancellationToken cancellationToken) =>
        operations.EnsureOperationalTaskWeekAsync(request, cancellationToken);
}

public sealed class StopMeasurementRecurrenceHandler(IWaterQualityOperations operations)
    : IRequestHandler<StopMeasurementRecurrenceCommand, bool>
{
    public Task<bool> Handle(StopMeasurementRecurrenceCommand request, CancellationToken cancellationToken) =>
        operations.StopMeasurementRecurrenceAsync(request, cancellationToken);
}

public sealed class AssignOperationalTaskHandler(IWaterQualityOperations operations)
    : IRequestHandler<AssignOperationalTaskCommand, PoolDutyScheduleDto>
{
    public Task<PoolDutyScheduleDto> Handle(AssignOperationalTaskCommand request, CancellationToken cancellationToken) =>
        operations.AssignOperationalTaskAsync(request, cancellationToken);
}

public sealed class CancelOperationalTaskHandler(IWaterQualityOperations operations)
    : IRequestHandler<CancelOperationalTaskCommand, PoolDutyScheduleDto>
{
    public Task<PoolDutyScheduleDto> Handle(CancelOperationalTaskCommand request, CancellationToken cancellationToken) =>
        operations.CancelOperationalTaskAsync(request, cancellationToken);
}
