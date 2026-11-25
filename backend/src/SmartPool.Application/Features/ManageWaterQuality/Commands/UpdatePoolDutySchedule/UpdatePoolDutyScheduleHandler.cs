using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;

public sealed class UpdatePoolDutyScheduleHandler : IRequestHandler<UpdatePoolDutyScheduleCommand, PoolDutyScheduleDto>
{
    private readonly IWaterQualityOperations _operations;
    private readonly IValidator<UpdatePoolDutyScheduleCommand> _validator;
    private readonly INotificationOperations _notificationOperations;
    private readonly INotificationService? _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdatePoolDutyScheduleHandler> _logger;

    public UpdatePoolDutyScheduleHandler(
        IWaterQualityOperations operations,
        IValidator<UpdatePoolDutyScheduleCommand> validator,
        INotificationOperations notificationOperations,
        TimeProvider timeProvider,
        ILogger<UpdatePoolDutyScheduleHandler> logger,
        INotificationService? notificationService = null)
    {
        _operations = operations;
        _validator = validator;
        _notificationOperations = notificationOperations;
        _timeProvider = timeProvider;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<PoolDutyScheduleDto> Handle(UpdatePoolDutyScheduleCommand request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var updated = await _operations.SavePoolDutyScheduleAsync(request, cancellationToken);

        try
        {
            if (_notificationService is not null)
            {
                await _notificationService.SendToAllAsync("WaterQualityUpdated", new
                {
                    type = "POOL_DUTY_UPDATED",
                    scheduleId = updated.Id,
                    dutyDate = updated.DutyDate
                }, cancellationToken);
            }

            var assignedEmployeeId = updated.AssignedEmployeeId;
            if (assignedEmployeeId is null) return updated;
            var notif = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = assignedEmployeeId.Value,
                Title = "Cập nhật lịch trực bể",
                Content = $"Lịch trực bể ngày {updated.DutyDate:dd/MM/yyyy} ({updated.ShiftName}) của bạn đã được cập nhật (Trạng thái: {updated.Status}).",
                Type = "POOL_DUTY_UPDATED",
                IsRead = false,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
            await _notificationOperations.AddAsync(notif, cancellationToken);

            if (_notificationService is not null)
            {
                await _notificationService.SendToUserAsync(
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[UpdatePoolDutyScheduleHandler] Không thể gửi thông báo cập nhật lịch trực bể {Id}", updated.Id);
        }

        return updated;
    }
}
