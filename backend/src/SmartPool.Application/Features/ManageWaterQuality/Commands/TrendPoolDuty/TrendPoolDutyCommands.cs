using System.Text.Json.Serialization;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.TrendPoolDuty;

public sealed record AssignTrendPoolDutyCommand(Guid ScheduleId, Guid AssignedEmployeeId, Guid? ShiftId)
    : IRequest<PoolDutyScheduleDto>
{
    [JsonIgnore] public Guid ActorId { get; set; }
}

public sealed record CompleteTrendPoolDutyCommand(Guid ScheduleId, Guid? WaterQualityLogId = null)
    : IRequest<PoolDutyScheduleDto>
{
    [JsonIgnore] public Guid ActorId { get; set; }
}

public sealed class AssignTrendPoolDutyHandler(
    IWaterQualityOperations operations,
    INotificationOperations notificationOperations,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<AssignTrendPoolDutyHandler> logger)
    : IRequestHandler<AssignTrendPoolDutyCommand, PoolDutyScheduleDto>
{
    public async Task<PoolDutyScheduleDto> Handle(AssignTrendPoolDutyCommand request, CancellationToken cancellationToken)
    {
        var updated = await operations.AssignTrendPoolDutyScheduleAsync(
            request.ScheduleId, request.AssignedEmployeeId, request.ShiftId, request.ActorId, cancellationToken);

        try
        {
            if (updated.AssignedEmployeeId.HasValue)
            {
                var title = "Bạn được phân công Lịch trực bể (Xu hướng 3 ngày)";
                var chemicalsText = updated.SuggestedChemicals.Count > 0
                    ? $" Hóa chất đề xuất: {string.Join(", ", updated.SuggestedChemicals)}."
                    : string.Empty;
                var content = $"Admin đã phân công bạn xử lý nước ngày {updated.DutyDate:dd/MM/yyyy}.{chemicalsText}";

                var notif = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = updated.AssignedEmployeeId.Value,
                    Title = title,
                    Content = content,
                    Type = "WATER_QUALITY_TREND_DUTY",
                    IsRead = false,
                    CreatedAt = timeProvider.GetUtcNow().UtcDateTime
                };
                await notificationOperations.AddRangeAsync([notif], cancellationToken);
                await notificationService.SendToUserAsync(
                    notif.UserId.ToString(),
                    "ReceiveNotification",
                    new
                    {
                        id = notif.Id,
                        title = notif.Title,
                        content = notif.Content,
                        type = notif.Type,
                        isRead = false,
                        createdAt = notif.CreatedAt
                    },
                    cancellationToken);
            }

            await notificationService.SendToAllAsync("WaterQualityUpdated", new
            {
                scheduleId = updated.Id,
                dutyDate = updated.DutyDate,
                status = updated.Status,
                isTrendDuty = true
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AssignTrendPoolDutyHandler] Lỗi gửi thông báo phân công lịch trực bể {Id}", updated.Id);
        }

        return updated;
    }
}

public sealed class CompleteTrendPoolDutyHandler(
    IWaterQualityOperations operations,
    INotificationService notificationService,
    ILogger<CompleteTrendPoolDutyHandler> logger)
    : IRequestHandler<CompleteTrendPoolDutyCommand, PoolDutyScheduleDto>
{
    public async Task<PoolDutyScheduleDto> Handle(CompleteTrendPoolDutyCommand request, CancellationToken cancellationToken)
    {
        var updated = await operations.CompleteTrendPoolDutyScheduleAsync(
            request.ScheduleId, request.ActorId, request.WaterQualityLogId, cancellationToken);

        try
        {
            await notificationService.SendToAllAsync("WaterQualityUpdated", new
            {
                scheduleId = updated.Id,
                dutyDate = updated.DutyDate,
                status = updated.Status,
                isTrendDuty = true
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CompleteTrendPoolDutyHandler] Lỗi broadcast WaterQualityUpdated cho {Id}", updated.Id);
        }

        return updated;
    }
}
