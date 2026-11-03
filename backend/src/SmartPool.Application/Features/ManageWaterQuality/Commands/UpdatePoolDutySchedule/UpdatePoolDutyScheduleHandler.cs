using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;

public sealed class UpdatePoolDutyScheduleHandler : IRequestHandler<UpdatePoolDutyScheduleCommand, PoolDutyScheduleDto>
{
    private readonly IWaterQualityOperations _operations;
    private readonly IValidator<UpdatePoolDutyScheduleCommand> _validator;
    private readonly IRepository<Notification> _notificationRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdatePoolDutyScheduleHandler> _logger;

    public UpdatePoolDutyScheduleHandler(
        IWaterQualityOperations operations,
        IValidator<UpdatePoolDutyScheduleCommand> validator,
        IRepository<Notification> notificationRepo,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<UpdatePoolDutyScheduleHandler> logger)
    {
        _operations = operations;
        _validator = validator;
        _notificationRepo = notificationRepo;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PoolDutyScheduleDto> Handle(UpdatePoolDutyScheduleCommand request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var updated = await _operations.SavePoolDutyScheduleAsync(request, cancellationToken);

        try
        {
            var notif = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = updated.AssignedEmployeeId,
                Title = "Cập nhật lịch trực bể",
                Content = $"Lịch trực bể ngày {updated.DutyDate:dd/MM/yyyy} ({updated.ShiftName}) của bạn đã được cập nhật (Trạng thái: {updated.Status}).",
                Type = "POOL_DUTY_UPDATED",
                IsRead = false,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
            await _notificationRepo.AddAsync(notif, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[UpdatePoolDutyScheduleHandler] Không thể gửi thông báo cập nhật lịch trực bể {Id}", updated.Id);
        }

        return updated;
    }
}

