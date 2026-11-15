namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed class RiskOptions
{
    public double WindowSeconds { get; set; } = 5;
    public double ConfidenceThreshold { get; set; } = 0.60;
    public double WarningRatio { get; set; } = 0.40;
    public double CriticalRatio { get; set; } = 0.70;
    public double CriticalHoldSeconds { get; set; } = 3;
    public double RecoveryRatio { get; set; } = 0.20;
    public double RecoveryHoldSeconds { get; set; } = 5;
    public double MaxObservationGapSeconds { get; set; } = 3;
    public double CooldownSeconds { get; set; } = 15;
    public int MinObservations { get; set; } = 3;
    public bool IsValid() => WindowSeconds > 0 && ConfidenceThreshold is >= 0 and <= 1 &&
        RecoveryRatio >= 0 && RecoveryRatio < WarningRatio && WarningRatio < CriticalRatio && CriticalRatio <= 1 &&
        CriticalHoldSeconds > 0 && RecoveryHoldSeconds > 0 && MaxObservationGapSeconds > 0 &&
        CooldownSeconds >= 0 && MinObservations >= 2;

    public RiskOptions Clone() => new()
    {
        WindowSeconds = WindowSeconds,
        ConfidenceThreshold = ConfidenceThreshold,
        WarningRatio = WarningRatio,
        CriticalRatio = CriticalRatio,
        CriticalHoldSeconds = CriticalHoldSeconds,
        RecoveryRatio = RecoveryRatio,
        RecoveryHoldSeconds = RecoveryHoldSeconds,
        MaxObservationGapSeconds = MaxObservationGapSeconds,
        CooldownSeconds = CooldownSeconds,
        MinObservations = MinObservations,
    };

    // Ghi đè toàn bộ giá trị vào instance hiện tại. Provider dùng để "nóng" đổi ngưỡng
    // mà không tạo instance mới (giữ nguyên state theo dõi trong engine).
    public void CopyFrom(RiskOptions s)
    {
        WindowSeconds = s.WindowSeconds;
        ConfidenceThreshold = s.ConfidenceThreshold;
        WarningRatio = s.WarningRatio;
        CriticalRatio = s.CriticalRatio;
        CriticalHoldSeconds = s.CriticalHoldSeconds;
        RecoveryRatio = s.RecoveryRatio;
        RecoveryHoldSeconds = s.RecoveryHoldSeconds;
        MaxObservationGapSeconds = s.MaxObservationGapSeconds;
        CooldownSeconds = s.CooldownSeconds;
        MinObservations = s.MinObservations;
    }
}

public sealed record TrackedDetection(string TrackId, string Class, double Confidence, double[] Box);
public sealed record RiskDecision(string TrackId, string Episode, string Severity, double Ratio, double Timestamp, double[] Box);

// One engine per camera/video; timestamps are source timestamps, never inference duration.
public sealed class RiskDecisionEngine(RiskOptions options)
{
    private sealed class State
    {
        public Queue<(double Time, bool Drowning)> Window { get; } = new();
        public string Episode { get; set; } = Guid.NewGuid().ToString("N");
        public string Severity { get; set; } = "NORMAL";
        public double Last { get; set; } = double.NegativeInfinity;
        public double? CriticalSince { get; set; }
        public double? RecoverySince { get; set; }
        public double CooldownUntil { get; set; }
    }
    private readonly Dictionary<string, State> states = new();

    public IReadOnlyList<RiskDecision> Observe(double timestamp, IEnumerable<TrackedDetection> detections)
    {
        if (!double.IsFinite(timestamp)) throw new ArgumentOutOfRangeException(nameof(timestamp));
        // Absence never emits NORMAL or resolves an alert.
        foreach (var key in states.Where(x => timestamp - x.Value.Last > 120).Select(x => x.Key).ToArray()) states.Remove(key);
        var decisions = new List<RiskDecision>();
        foreach (var detection in detections.GroupBy(x => x.TrackId).Select(x => x.MaxBy(d => d.Confidence)!))
        {
            if (detection.Class is not ("normal" or "drowning") || !double.IsFinite(detection.Confidence) ||
                detection.Confidence < options.ConfidenceThreshold || detection.Confidence > 1 ||
                detection.Box.Length != 4 || detection.Box.Any(x => !double.IsFinite(x) || x < 0 || x > 1) ||
                detection.Box[2] <= detection.Box[0] || detection.Box[3] <= detection.Box[1] || string.IsNullOrWhiteSpace(detection.TrackId)) continue;
            if (!states.TryGetValue(detection.TrackId, out var state))
            {
                // Bound memory on videos with rapidly changing IDs. Eviction never resolves a persisted alert.
                if (states.Count >= 4096)
                    foreach (var key in states.OrderBy(x => x.Value.Last).Take(1024).Select(x => x.Key).ToArray()) states.Remove(key);
                states[detection.TrackId] = state = new State();
            }
            if (timestamp <= state.Last) continue;
            if (timestamp - state.Last > options.MaxObservationGapSeconds)
            {
                state.Window.Clear(); state.CriticalSince = null; state.RecoverySince = null;
                // Retain severity and episode across a gap. Unknown is not recovery.
            }
            state.Last = timestamp;
            state.Window.Enqueue((timestamp, detection.Class == "drowning"));
            while (state.Window.Count > 0 && timestamp - state.Window.Peek().Time > options.WindowSeconds) state.Window.Dequeue();
            if (state.Window.Count < options.MinObservations) continue;
            var ratio = state.Window.Count(x => x.Drowning) / (double)state.Window.Count;
            if (ratio >= options.WarningRatio && timestamp >= state.CooldownUntil && state.Severity == "NORMAL") state.Severity = "WARNING";
            if (ratio >= options.CriticalRatio && timestamp >= state.CooldownUntil)
            {
                state.CriticalSince ??= timestamp;
                if (timestamp - state.CriticalSince >= options.CriticalHoldSeconds) state.Severity = "CRITICAL";
            }
            else state.CriticalSince = null;
            if (ratio <= options.RecoveryRatio)
            {
                state.RecoverySince ??= timestamp;
                if (timestamp - state.RecoverySince >= options.RecoveryHoldSeconds && state.Severity != "NORMAL")
                {
                    state.Severity = "NORMAL"; state.Episode = Guid.NewGuid().ToString("N");
                    state.CooldownUntil = timestamp + options.CooldownSeconds; state.CriticalSince = null;
                }
            }
            else state.RecoverySince = null;
            if (state.Severity != "NORMAL") decisions.Add(new(detection.TrackId, state.Episode, state.Severity, ratio, timestamp, detection.Box));
        }
        return decisions;
    }
}
