using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Commands.ActivateSafetyAlert;

/// <summary>
/// Handler: tạo payload cảnh báo, lưu Incident vào cơ sở dữ liệu và broadcast SignalR tới tất cả clients.
/// </summary>
public sealed class ActivateSafetyAlertHandler : IRequestHandler<ActivateSafetyAlertCommand, ActivateSafetyAlertResult>
{
    private readonly INotificationService _notificationService;
    private readonly IRepository<Incident> _incidentRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateSafetyAlertHandler(
        INotificationService notificationService,
        IRepository<Incident> incidentRepo,
        IUnitOfWork unitOfWork)
    {
        _notificationService = notificationService;
        _incidentRepo = incidentRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ActivateSafetyAlertResult> Handle(
        ActivateSafetyAlertCommand request,
        CancellationToken cancellationToken)
    {
        var alertId = Guid.NewGuid();
        var triggeredAt = DateTime.UtcNow;

        var result = new ActivateSafetyAlertResult
        {
            AlertId = alertId,
            Zone = request.Zone,
            Level = request.Level,
            Description = request.Description,
            TriggeredByName = request.TriggeredByName,
            TriggeredAt = triggeredAt,
        };

        // 1. Lưu bản ghi sự cố vào DB
        try
        {
            var incident = new Incident
            {
                Id = alertId,
                Title = $"Cảnh báo cứu hộ: {request.Zone}",
                Description = request.Description,
                SeverityLevel = request.Level,
                IncidentTime = triggeredAt,
                ReportedBy = Guid.TryParse(request.TriggeredByUserId, out var uid) ? uid : null,
                ImageIncidents = request.ImageIncidents,
                Status = "Resolved",
                CreatedAt = triggeredAt,
                UpdatedAt = triggeredAt,
            };

            await _incidentRepo.AddAsync(incident, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ActivateSafetyAlertHandler] Warning saving incident to DB: {ex.Message}");
        }

        // 2. Broadcast tới tất cả clients kết nối SignalR
        await _notificationService.SendToAllAsync(
            eventName: "safety_alert",
            payload: result,
            cancellationToken: cancellationToken
        );

        return result;
    }
}
