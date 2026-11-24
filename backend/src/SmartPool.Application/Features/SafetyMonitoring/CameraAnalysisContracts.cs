using MediatR;

namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed record GetCameraAnalysisQuery(Guid Id) : IRequest<CameraAnalysisDto>;

public sealed record GetCameraAnalysisFrameQuery(Guid Id, string FrameId) : IRequest<byte[]?>;

public sealed record CameraAnalysisDto(string State, string? FrameId, DateTimeOffset? ObservedAt, DateTimeOffset? ProcessedAt,
    IReadOnlyList<RiskDecision> Risks, string Synchronization = "PAIRED_FRAME", IReadOnlyList<TrackedDetection>? Detections = null)
{
    public DateTimeOffset ServerTime { get; init; } = DateTimeOffset.UtcNow;
    public Guid? CameraId { get; init; }
    public double? InferenceMilliseconds { get; init; }
    public double? RenderMilliseconds { get; init; }
    // Capture/AI/risk/render up to metadata publication; excludes upload and loop delay.
    public double? AnalysisMilliseconds { get; init; }
    public uint? RtpTimestamp { get; init; }
    public string? TrackingSession { get; init; }
    public IReadOnlyList<string>? ActiveRiskTrackIds { get; init; }
    // Current measured geometry with a latched risk colour, for new viewers.
    // Display-only; Risks remains the actual evidence from RiskDecisionEngine.
    public IReadOnlyList<RiskDecision>? DisplayRisks { get; init; }
}

public interface ICameraAnalysisReader
{
    CameraAnalysisDto Get(Guid id);
    byte[]? Frame(Guid id, string frameId);
}

public sealed class GetCameraAnalysisHandler(ICameraAnalysisReader reader) : IRequestHandler<GetCameraAnalysisQuery, CameraAnalysisDto>
{
    public Task<CameraAnalysisDto> Handle(GetCameraAnalysisQuery request, CancellationToken ct) => Task.FromResult(reader.Get(request.Id));
}

public sealed class GetCameraAnalysisFrameHandler(ICameraAnalysisReader reader) : IRequestHandler<GetCameraAnalysisFrameQuery, byte[]?>
{
    public Task<byte[]?> Handle(GetCameraAnalysisFrameQuery request, CancellationToken ct) => Task.FromResult(reader.Frame(request.Id, request.FrameId));
}
