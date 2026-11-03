using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;
using SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutySchedules;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityAnalytics;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogs;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IWaterQualityOperations
{
    Task<WaterQualityLogDto> CreateWaterQualityLogAsync(WaterQualityLog log, CancellationToken cancellationToken);
    Task<PagedResponse<WaterQualityLogDto>> GetWaterQualityLogsAsync(GetWaterQualityLogsQuery query, CancellationToken cancellationToken);
    Task<WaterQualityLogDto?> GetWaterQualityLogByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<WaterQualityAnalyticsResponse> GetWaterQualityAnalyticsAsync(GetWaterQualityAnalyticsQuery query, CancellationToken cancellationToken);

    Task<PoolDutyOptionsResponse> GetPoolDutyOptionsAsync(CancellationToken cancellationToken);
    Task<PagedResponse<PoolDutyScheduleDto>> GetPoolDutySchedulesAsync(GetPoolDutySchedulesQuery query, CancellationToken cancellationToken);
    Task<AutoAssignPoolDutyResultDto> AutoAssignPoolDutySchedulesAsync(AutoAssignPoolDutyCommand command, CancellationToken cancellationToken);
    Task<PoolDutyScheduleDto> SavePoolDutyScheduleAsync(UpdatePoolDutyScheduleCommand command, CancellationToken cancellationToken);
}
