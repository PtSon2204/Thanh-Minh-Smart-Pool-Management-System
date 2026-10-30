using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
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
    private readonly IRepository<Notification> _notificationRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateWaterQualityLogHandler> _logger;

    public CreateWaterQualityLogHandler(
        IWaterQualityOperations operations,
        IWaterQualityEvaluator evaluator,
        IValidator<CreateWaterQualityLogCommand> validator,
        ICurrentUser currentUser,
        INotificationService notificationService,
        IRepository<Notification> notificationRepo,
        IRepository<User> userRepo,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<CreateWaterQualityLogHandler> logger)
    {
        _operations = operations;
        _evaluator = evaluator;
        _validator = validator;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _notificationRepo = notificationRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
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

        var evaluation = _evaluator.Evaluate(request.PhValue, request.FreeChlorineMgL, request.IsWaterClear);

        var log = new WaterQualityLog
        {
            Id = Guid.NewGuid(),
            InspectionTime = request.InspectionTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.InspectionTime, DateTimeKind.Utc)
                : request.InspectionTime.ToUniversalTime(),
            PhValue = request.PhValue,
            FreeChlorineMgL = request.FreeChlorineMgL,
            IsWaterClear = request.IsWaterClear,
            Source = "Manual",
            InspectedBy = inspectorId.Value,
            EquipmentId = request.EquipmentId,
            SourceEventId = null,
            Status = evaluation.Status,
            Recommendation = evaluation.Recommendation,
            EvaluationVersion = evaluation.EvaluationVersion,
            Notes = request.Notes,
            CreatedAt = nowUtc
        };

        var createdDto = await _operations.CreateWaterQualityLogAsync(log, cancellationToken);

        // Nếu trạng thái là Danger, phát cảnh báo khẩn cấp tới Admin
        if (evaluation.Status == WaterQualityConstants.StatusDanger)
        {
            await TriggerDangerAlertAsync(createdDto, cancellationToken);
        }

        return createdDto;
    }

    private async Task TriggerDangerAlertAsync(WaterQualityLogDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var title = "CẢNH BÁO NƯỚC BỂ NGUY HIỂM";
            var content = $"Phiếu đo lúc {dto.InspectionTime:dd/MM/yyyy HH:mm} vượt ngưỡng an toàn nghiêm trọng (pH: {dto.PhValue:F2}, Clo: {dto.FreeChlorineMgL:F2} mg/L). {dto.Recommendation}";

            var admins = await _userRepo.FindAsync(u => u.Role != null && u.Role.Name.ToUpper() == "ADMIN" && u.IsDeleted != true, cancellationToken);
            foreach (var admin in admins)
            {
                var notif = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = admin.Id,
                    Title = title,
                    Content = content,
                    Type = "WATER_QUALITY_ALERT",
                    IsRead = false,
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                };
                await _notificationRepo.AddAsync(notif, cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
