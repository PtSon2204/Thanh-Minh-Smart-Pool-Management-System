using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;
using SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutySchedules;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityAnalytics;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogs;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IWaterQualityOperations
{
    Task<WaterQualityLogDto> CreateWaterQualityLogAsync(WaterQualityLog log, CancellationToken cancellationToken,
        OperationalTaskExecutionAuthorization? taskExecution);
    Task<PagedResponse<WaterQualityLogDto>> GetWaterQualityLogsAsync(GetWaterQualityLogsQuery query, CancellationToken cancellationToken);
    Task<WaterQualityLogDto?> GetWaterQualityLogByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<WaterQualityAnalyticsResponse> GetWaterQualityAnalyticsAsync(GetWaterQualityAnalyticsQuery query, CancellationToken cancellationToken);

    Task<PoolDutyOptionsResponse> GetPoolDutyOptionsAsync(CancellationToken cancellationToken);
    Task<PagedResponse<PoolDutyScheduleDto>> GetPoolDutySchedulesAsync(GetPoolDutySchedulesQuery query, CancellationToken cancellationToken);
    Task<AutoAssignPoolDutyResultDto> AutoAssignPoolDutySchedulesAsync(AutoAssignPoolDutyCommand command, CancellationToken cancellationToken);
    Task<PoolDutyScheduleDto> SavePoolDutyScheduleAsync(UpdatePoolDutyScheduleCommand command, CancellationToken cancellationToken);
    Task<PagedResponse<PoolDutyScheduleDto>> GetOperationalTasksAsync(GetOperationalTasksQuery query, CancellationToken cancellationToken);
    Task<PoolDutySchedule?> GetOperationalTaskByIdAsync(Guid taskId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PoolDutyEligibleEmployeeDto>> GetOperationalTaskAssigneesAsync(Guid taskId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PoolDutyMeasurementRecurrenceDto>> GetMeasurementRecurrencesAsync(CancellationToken cancellationToken);
    Task<OperationalTaskWeekConfigurationDto?> GetOperationalTaskWeekConfigurationAsync(DateOnly weekStart,
        CancellationToken cancellationToken);
    Task<int> EnsureChecklistTasksAsync(DateOnly date, CancellationToken cancellationToken);
    Task<EnsureOperationalTaskWeekResultDto> EnsureOperationalTaskWeekAsync(EnsureOperationalTaskWeekCommand command, CancellationToken cancellationToken);
    Task CompleteOperationalChecklistTaskAsync(DailyInspection inspection,
        OperationalTaskExecutionAuthorization authorization, CancellationToken cancellationToken);
    Task<DailyInspectionDto?> GetOperationalTaskChecklistResultAsync(Guid taskId, Guid? employeeId,
        CancellationToken cancellationToken);
    Task<bool> StopMeasurementRecurrenceAsync(StopMeasurementRecurrenceCommand command, CancellationToken cancellationToken);
    Task<PoolDutyScheduleDto> AssignOperationalTaskAsync(AssignOperationalTaskCommand command, CancellationToken cancellationToken);
    Task<PoolDutyScheduleDto> CancelOperationalTaskAsync(CancelOperationalTaskCommand command, CancellationToken cancellationToken);
    Task MaterializeDueOperationalTasksAsync(CancellationToken cancellationToken);
}
