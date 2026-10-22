namespace SmartPool.Application.Features.ManageSafetyAlerts.Commands.ActivateSafetyAlert;

public sealed record ActivateSafetyAlertResult
{
    public Guid AlertId { get; init; }
    public string Zone { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string TriggeredByName { get; init; } = string.Empty;
    public DateTime TriggeredAt { get; init; }
}
