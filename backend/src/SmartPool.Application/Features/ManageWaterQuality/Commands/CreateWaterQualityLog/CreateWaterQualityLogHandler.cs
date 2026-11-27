using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Application.Features.ManageWaterQuality.Services;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.CreateWaterQualityLog;

public sealed class CreateWaterQualityLogHandler : IRequestHandler<CreateWaterQualityLogCommand, WaterQualityLogDto>
{
    private readonly IWaterQualityOperations _operations;
    private readonly IWaterQualityEvaluator _evaluator;
    private readonly IValidator<CreateWaterQualityLogCommand> _validator;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notificationService;
    private readonly INotificationOperations _notificationOperations;
    private readonly IUserOperations _userOperations;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateWaterQualityLogHandler> _logger;

    public CreateWaterQualityLogHandler(
        IWaterQualityOperations operations,
        IWaterQualityEvaluator evaluator,
        IValidator<CreateWaterQualityLogCommand> validator,
        ICurrentUser currentUser,
        INotificationService notificationService,
        INotificationOperations notificationOperations,
        IUserOperations userOperations,
        TimeProvider timeProvider,
        ILogger<CreateWaterQualityLogHandler> logger)
    {
        _operations = operations;
        _evaluator = evaluator;
        _validator = validator;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _notificationOperations = notificationOperations;
        _userOperations = userOperations;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<WaterQualityLogDto> Handle(CreateWaterQualityLogCommand request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var inspectorId = _currentUser.UserId;
        if (!inspectorId.HasValue || inspectorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Người đo không hợp lệ hoặc phiên đăng nhập đã hết hạn.");
        }
        var isLifeguard = _currentUser.IsInRole("LIFEGUARD");
        if (!isLifeguard)
        {
            throw new UnauthorizedAccessException("Chỉ nhân viên cứu hộ mới có quyền thực hiện đo chất lượng nước.");
        }

        var evaluation = _evaluator.Evaluate(request.PhValue, request.FreeChlorineMgL, request.IsWaterClear);

        var log = new WaterQualityLog
        {
            Id = Guid.NewGuid(),
            InspectionTime = nowUtc, // Luôn ghi nhận thời gian thực tế đo (real-time UTC)
            PhValue = request.PhValue,
            FreeChlorineMgL = request.FreeChlorineMgL,
            IsWaterClear = request.IsWaterClear,
            Source = "Manual",
            InspectedBy = inspectorId.Value,
            EquipmentId = request.EquipmentId,
            OperationalTaskId = request.OperationalTaskId,
            SourceEventId = null,
            Status = evaluation.Status,
            Recommendation = evaluation.Recommendation,
            EvaluationVersion = evaluation.EvaluationVersion,
            Notes = request.Notes,
            CreatedAt = nowUtc
        };

        var taskExecution = request.OperationalTaskId.HasValue
            ? new OperationalTaskExecutionAuthorization(false, true, false,
                request.ExpectedTaskRevision, request.ExpectedAssigneeId)
            : null;
        var createdDto = await _operations.CreateWaterQualityLogAsync(log, cancellationToken, taskExecution);

        await BroadcastWaterQualityUpdatedAsync(createdDto, cancellationToken);

        // Nếu trạng thái là Danger, phát cảnh báo khẩn cấp tới Admin
        if (evaluation.Status == WaterQualityConstants.StatusDanger)
        {
            await TriggerDangerAlertAsync(createdDto, cancellationToken);
        }

        // Chỉ gửi thông báo phân công Lịch trực bể ngày mai khi chỉ số hôm nay vẫn an toàn và hệ thống thực sự tạo lịch D+1
        if (createdDto.ThreeDayTrendDetected && createdDto.GeneratedNextDayDutyId.HasValue)
        {
            await TriggerTrendDutyNoticeAsync(createdDto, cancellationToken);
        }

        return createdDto;
    }

    private async Task BroadcastWaterQualityUpdatedAsync(WaterQualityLogDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.SendToAllAsync("WaterQualityUpdated", new
            {
                id = dto.Id,
                status = dto.Status,
                phValue = dto.PhValue,
                freeChlorineMgL = dto.FreeChlorineMgL,
                inspectionTime = dto.InspectionTime,
                operationalTaskId = dto.OperationalTaskId,
                threeDayTrendDetected = dto.ThreeDayTrendDetected,
                generatedNextDayDutyId = dto.GeneratedNextDayDutyId,
                generatedNextDayDutyDate = dto.GeneratedNextDayDutyDate
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CreateWaterQualityLogHandler] Không thể phát SignalR WaterQualityUpdated cho log {Id}", dto.Id);
        }
    }

    private async Task TriggerTrendDutyNoticeAsync(WaterQualityLogDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var admins = await _userOperations.GetUsersByRoleAsync("ADMIN", cancellationToken);
            if (admins.Count == 0)
            {
                return;
            }

            var dutyDateText = dto.GeneratedNextDayDutyDate.HasValue
                ? dto.GeneratedNextDayDutyDate.Value.ToString("dd/MM/yyyy")
                : "ngày mai";
            var chemicalsText = dto.TrendSuggestedChemicals.Count > 0
                ? $" Hóa chất đề xuất: {string.Join(", ", dto.TrendSuggestedChemicals)}."
                : string.Empty;
            var title = "PHÁT HIỆN NƯỚC ĐI XUỐNG 3 NGÀY — ĐÃ TẠO LỊCH TRỰC BỂ";
            var content = $"{dto.ThreeDayTrendSummary}{chemicalsText} Hệ thống đã tạo lịch trực bể ngày {dutyDateText} ở trạng thái chờ Admin phân công.";

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var notifs = admins.Select(admin => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = admin.Id,
                Title = title,
                Content = content,
                Type = "WATER_QUALITY_TREND_DUTY",
                IsRead = false,
                CreatedAt = now
            }).ToList();

            await _notificationOperations.AddRangeAsync(notifs, cancellationToken);

            foreach (var notif in notifs)
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
            _logger.LogWarning(ex, "[CreateWaterQualityLogHandler] Không thể gửi thông báo lịch trực xu hướng 3 ngày cho log {Id}", dto.Id);
        }
    }

    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private async Task TriggerDangerAlertAsync(WaterQualityLogDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(dto.InspectionTime, DateTimeKind.Utc), VietnamTimeZone);
            var title = "CẢNH BÁO NƯỚC BỂ NGUY HIỂM — CẦN XEM XÉT ĐÓNG CỬA BỂ";
            var content = $"Phiếu đo lúc {vnTime:dd/MM/yyyy HH:mm} vượt ngưỡng an toàn nghiêm trọng (pH: {dto.PhValue:F2}, Clo: {dto.FreeChlorineMgL:F2} mg/L). {dto.Recommendation} Đề xuất Admin kiểm tra ngay và xem xét tạm đóng cửa bể bơi để xử lý.";

            var admins = await _userOperations.GetUsersByRoleAsync("ADMIN", cancellationToken);
            var notifs = admins.Select(admin => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = admin.Id,
                Title = title,
                Content = content,
                Type = "WATER_QUALITY_ALERT",
                IsRead = false,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            }).ToList();

            if (notifs.Count > 0)
            {
                await _notificationOperations.AddRangeAsync(notifs, cancellationToken);

                foreach (var notif in notifs)
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

            await _notificationService.SendToAllAsync("water_quality_danger", new
            {
                dto.Id,
                dto.InspectionTime,
                dto.PhValue,
                dto.FreeChlorineMgL,
                dto.Status,
                dto.Recommendation,
                Title = title,
                Message = content
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CreateWaterQualityLogHandler] Không thể gửi cảnh báo SignalR/Notification cho Danger log {Id}", dto.Id);
        }
    }
}
