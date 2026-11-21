using System;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Commands.ConfirmSafetyIncident;

public sealed record ConfirmSafetyIncidentResult
{
    public Guid IncidentId { get; init; }
    public string Status { get; init; } = "Resolved";
    public string Message { get; init; } = "Xác nhận xử lý sự cố thành công";
    public DateTime ConfirmedAt { get; init; }
    public string ConfirmedByName { get; init; } = string.Empty;
}
