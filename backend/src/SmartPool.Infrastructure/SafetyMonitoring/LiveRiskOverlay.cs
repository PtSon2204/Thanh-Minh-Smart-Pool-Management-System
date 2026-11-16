using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

// Presentation state only. Never pass these carried boxes to RecordRiskCommand.
// One instance per camera/tracking session, alongside its decision engine.
public sealed class LiveRiskOverlay
{
    private readonly Dictionary<string, RiskDecision> latched = new();
    public void Clear() => latched.Clear();

    public (IReadOnlyList<RiskDecision> Rendered, IReadOnlyList<RiskDecision> Current) Update(double timestamp,
        IReadOnlyList<TrackedDetection> detections, IReadOnlyList<RiskDecision> decisions, IReadOnlyList<string> activeTrackIds)
    {
        if (!double.IsFinite(timestamp)) throw new ArgumentOutOfRangeException(nameof(timestamp));
        var active = activeTrackIds.ToHashSet(StringComparer.Ordinal);
        var measured = decisions.Where(x => x.Severity is "WARNING" or "CRITICAL" && ValidBox(x.Box)).ToArray();
        var observed = measured.Select(x => x.TrackId).ToHashSet(StringComparer.Ordinal);
        foreach (var id in latched.Keys.Where(id => !active.Contains(id) && !observed.Contains(id)).ToArray()) latched.Remove(id);
        foreach (var decision in measured) latched[decision.TrackId] = decision with { Timestamp = timestamp };
        foreach (var detection in detections.Where(x => x.Class is "normal" or "drowning" &&
            double.IsFinite(x.Confidence) && x.Confidence is > 0 and <= 1 && ValidBox(x.Box))
            .GroupBy(x => x.TrackId).Select(x => x.MaxBy(d => d.Confidence)!))
        {
            if (!active.Contains(detection.TrackId) || !latched.TryGetValue(detection.TrackId, out var risk) || timestamp < risk.Timestamp) continue;
            latched[detection.TrackId] = risk with { Timestamp = timestamp, Box = detection.Box };
        }
        var visible = latched.Values.Where(x => timestamp >= x.Timestamp && timestamp - x.Timestamp <= .3).ToArray();
        // Only current measured geometry is advertised to a NEW viewer. Missing
        // observations may render briefly, but must not renew a client hold TTL.
        return (visible, visible.Where(x => x.Timestamp == timestamp).ToArray());
    }

    private static bool ValidBox(double[] box) => box.Length == 4 && box.All(x => double.IsFinite(x) && x is >= 0 and <= 1)
        && box[2] > box[0] && box[3] > box[1];
}
