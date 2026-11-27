using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;

public sealed class AutoAssignPoolDutyHandler : IRequestHandler<AutoAssignPoolDutyCommand, AutoAssignPoolDutyResultDto>
{
    private readonly IWaterQualityOperations _operations;
    private readonly IValidator<AutoAssignPoolDutyCommand> _validator;
    private readonly INotificationOperations _notificationOperations;
    private readonly INotificationService? _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AutoAssignPoolDutyHandler> _logger;

    public AutoAssignPoolDutyHandler(
        IWaterQualityOperations operations,
        IValidator<AutoAssignPoolDutyCommand> validator,
        INotificationOperations notificationOperations,
        TimeProvider timeProvider,
        ILogger<AutoAssignPoolDutyHandler> logger,
        INotificationService? notificationService = null)
    {
        _operations = operations;
        _validator = validator;
        _notificationOperations = notificationOperations;
        _timeProvider = timeProvider;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<AutoAssignPoolDutyResultDto> Handle(AutoAssignPoolDutyCommand request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _operations.AutoAssignPoolDutySchedulesAsync(request, cancellationToken);

        // Gửi thông báo cho các nhân viên được phân công
        if (result.AssignedSchedules.Count > 0)
        {
            await NotifyAssignedEmployeesAsync(request, result.AssignedSchedules, cancellationToken);
        }

        try
        {
            if (_notificationService is not null)
            {
                await _notificationService.SendToAllAsync("WaterQualityUpdated", new
                {
                    type = "POOL_DUTY_ASSIGNED",
                    fromDate = request.FromDate,
                    toDate = request.ToDate
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AutoAssignPoolDutyHandler] Không thể phát SignalR WaterQualityUpdated.");
        }

        return result;
    }

    private async Task NotifyAssignedEmployeesAsync(
        AutoAssignPoolDutyCommand request,
        List<PoolDutyScheduleDto> schedules,
        CancellationToken cancellationToken)
    {
        try
        {
            var groupedByEmployee = schedules.Where(s => s.AssignedEmployeeId.HasValue)
                .GroupBy(s => s.AssignedEmployeeId.GetValueOrDefault());
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

            var notifs = new List<Notification>();
            foreach (var group in groupedByEmployee)
            {
                var count = group.Count();
                notifs.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = group.Key,
                    Title = "Phân công lịch trực bể & đo nước",
                    Content = $"Bạn đã được phân công {count} ca trực bể và kiểm tra chất lượng nước từ ngày {request.FromDate:dd/MM/yyyy} đến {request.ToDate:dd/MM/yyyy}.",
                    Type = "POOL_DUTY_ASSIGNED",
                    IsRead = false,
                    CreatedAt = nowUtc
                });
            }

            if (notifs.Count > 0)
            {
                await _notificationOperations.AddRangeAsync(notifs, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AutoAssignPoolDutyHandler] Không thể tạo thông báo phân công lịch trực bể.");
        }
    }
}
