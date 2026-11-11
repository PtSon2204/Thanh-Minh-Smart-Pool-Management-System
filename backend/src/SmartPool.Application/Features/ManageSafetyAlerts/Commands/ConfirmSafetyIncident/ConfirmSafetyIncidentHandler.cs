using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Commands.ConfirmSafetyIncident;

public sealed class ConfirmSafetyIncidentHandler : IRequestHandler<ConfirmSafetyIncidentCommand, ConfirmSafetyIncidentResult>
{
    private readonly ISafetyAlertOperations _operations;
    private readonly INotificationService _notificationService;

    public ConfirmSafetyIncidentHandler(
        ISafetyAlertOperations operations,
        INotificationService notificationService)
    {
        _operations = operations;
        _notificationService = notificationService;
    }

    public async Task<ConfirmSafetyIncidentResult> Handle(
        ConfirmSafetyIncidentCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Status is not ("Pending" or "In Progress" or "Resolved"))
            throw new ArgumentException("Trạng thái sự cố không hợp lệ.");
        var incident = await _operations.GetIncidentByIdAsync(request.IncidentId, cancellationToken);
        if (incident is null) throw new KeyNotFoundException("Không tìm thấy sự cố.");
        var now = DateTime.UtcNow;

        incident.Status = request.Status;
        incident.UpdatedAt = now;

        if (!string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            var noteText = $"[Xử lý bởi {request.ConfirmedByName} lúc {now:HH:mm dd/MM/yyyy}]: {request.ResolutionNote}";
            incident.Description = string.IsNullOrWhiteSpace(incident.Description)
                ? noteText
                : $"{incident.Description}\n{noteText}";
        }

        await _operations.UpdateIncidentAsync(incident, cancellationToken);

        var result = new ConfirmSafetyIncidentResult
        {
            IncidentId = request.IncidentId,
            Status = request.Status,
            Message = "Cập nhật và xác nhận xử lý sự cố thành công",
            ConfirmedAt = now,
            ConfirmedByName = request.ConfirmedByName,
        };

        // Broadcast realtime để tất cả thiết bị cập nhật lại danh sách sự cố
        await _notificationService.SendToAllAsync(
            "safety_incident_confirmed",
            result,
            cancellationToken);

        return result;
    }
}
