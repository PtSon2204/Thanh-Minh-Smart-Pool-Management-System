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
    private readonly IRepository<Incident> _incidentRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public ConfirmSafetyIncidentHandler(
        IRepository<Incident> incidentRepo,
        IUnitOfWork unitOfWork,
        INotificationService notificationService)
    {
        _incidentRepo = incidentRepo;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<ConfirmSafetyIncidentResult> Handle(
        ConfirmSafetyIncidentCommand request,
        CancellationToken cancellationToken)
    {
        var incident = await _incidentRepo.GetByIdAsync(request.IncidentId, cancellationToken);
        var now = DateTime.UtcNow;

        if (incident is not null)
        {
            incident.Status = request.Status;
            incident.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(request.ResolutionNote))
            {
                var noteText = $"[Xử lý bởi {request.ConfirmedByName} lúc {now:HH:mm dd/MM/yyyy}]: {request.ResolutionNote}";
                incident.Description = string.IsNullOrWhiteSpace(incident.Description)
                    ? noteText
                    : $"{incident.Description}\n{noteText}";
            }

            _incidentRepo.Update(incident);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var result = new ConfirmSafetyIncidentResult
        {
            IncidentId = request.IncidentId,
            Status = request.Status,
            Message = "Cập nhật và xác nhận xử lý sự cố thành công",
            ConfirmedAt = now,
            ConfirmedByName = request.ConfirmedByName,
        };

        // Broadcast realtime để tất cả thiết bị cập nhật lại danh sách sự cố
        try
        {
            await _notificationService.SendToAllAsync("incident_updated", result, cancellationToken);
        }
        catch
        {
            // ignore broadcast error
        }

        return result;
    }
}
