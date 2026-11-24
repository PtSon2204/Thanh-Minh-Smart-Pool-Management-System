namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed class MonitoringConflictException(string message, AlertDto? currentAlert = null) : Exception(message)
{
    public AlertDto? CurrentAlert { get; } = currentAlert;
}
