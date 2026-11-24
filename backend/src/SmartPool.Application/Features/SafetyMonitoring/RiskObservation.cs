namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed record TrackedDetection(string TrackId, string Class, double Confidence, double[] Box);
public sealed record RiskDecision(string TrackId, string Episode, string Severity, double Ratio, double Timestamp, double[] Box);
