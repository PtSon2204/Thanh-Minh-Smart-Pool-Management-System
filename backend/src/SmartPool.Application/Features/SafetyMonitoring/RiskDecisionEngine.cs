namespace SmartPool.Application.Features.SafetyMonitoring;

// One engine per camera; timestamps are source timestamps, never inference duration.
public sealed class RiskDecisionEngine(RiskOptions options)
{
    // Temporal state belongs to one track; observation gaps never imply recovery.
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
    private const double TrackRetentionSeconds = 120;
    private const int MaxTrackedStates = 4096;
    private const int EvictionBatchSize = 1024;
    private readonly Dictionary<string, State> states = new();

    // Display continuity only: skipped low-confidence/missing observations do not
    // resolve a latched risk. These IDs must never be persisted as new observations.
    public IReadOnlyList<string> ActiveTrackIds() => states.Where(x => x.Value.Severity != "NORMAL").Select(x => x.Key).ToArray();

    public IReadOnlyList<RiskDecision> Observe(double timestamp, IEnumerable<TrackedDetection> detections)
    {
        if (!double.IsFinite(timestamp)) throw new ArgumentOutOfRangeException(nameof(timestamp));
        // One coherent threshold version for this entire observation, shared
        // safely with CopyFrom without resetting the track's temporal state.
        var thresholds = options.Clone();
        // Absence never emits NORMAL or resolves an alert.
        foreach (var key in states.Where(x => timestamp - x.Value.Last > TrackRetentionSeconds).Select(x => x.Key).ToArray()) states.Remove(key);
        var decisions = new List<RiskDecision>();
        foreach (var detection in detections.GroupBy(x => x.TrackId).Select(x => x.MaxBy(d => d.Confidence)!))
        // Gom detection theo TrackId.
        // Nếu một TrackId xuất hiện nhiều lần trong cùng frame,
        // chỉ chọn detection có confidence cao nhất để tránh đếm trùng.
        {
            // Chỉ nhận detection hợp lệ: đúng class, confidence đạt ngưỡng,
            // box chuẩn hóa trong [0,1], tọa độ có diện tích dương và TrackId hợp lệ.
            // Detection không hợp lệ sẽ bị bỏ qua, không được tính là normal.
            if (detection.Class is not ("normal" or "drowning") || !double.IsFinite(detection.Confidence) ||
                detection.Confidence < thresholds.ConfidenceThreshold || detection.Confidence > 1 ||
                detection.Box.Length != 4 || detection.Box.Any(x => !double.IsFinite(x) || x < 0 || x > 1) ||
                detection.Box[2] <= detection.Box[0] || detection.Box[3] <= detection.Box[1] || string.IsNullOrWhiteSpace(detection.TrackId)) continue;
            if (!states.TryGetValue(detection.TrackId, out var state))
            {
                // Bound memory on videos with rapidly changing IDs. Eviction never resolves a persisted alert.
                if (states.Count >= MaxTrackedStates)
                    foreach (var key in states.OrderBy(x => x.Value.Last).Take(EvictionBatchSize).Select(x => x.Key).ToArray()) states.Remove(key);
                states[detection.TrackId] = state = new State();
            }
            if (timestamp <= state.Last) continue;
            if (timestamp - state.Last > thresholds.MaxObservationGapSeconds)
            {
                state.Window.Clear();
                state.CriticalSince = null;
                state.RecoverySince = null;
                // Retain severity and episode across a gap. Unknown is not recovery.
            }
            state.Last = timestamp;
            // Sliding Window:
            // 1. Thêm observation của frame hiện tại.
            // 2. Loại observation cũ hơn WindowSeconds.
            // 3. Chỉ đánh giá khi có ít nhất MinObservations.
            // 4. Ratio = số observation drowning / tổng observation hợp lệ.
            state.Window.Enqueue((timestamp, detection.Class == "drowning"));
            while (state.Window.Count > 0 && timestamp - state.Window.Peek().Time > thresholds.WindowSeconds) state.Window.Dequeue();
            if (state.Window.Count < thresholds.MinObservations) continue;
            var ratio = state.Window.Count(x => x.Drowning) / (double)state.Window.Count;
            // Configured ratios and source-time hold durations govern escalation.
            // Resetting the hold timer never downgrades an existing critical risk.
            if (ratio >= thresholds.WarningRatio && timestamp >= state.CooldownUntil && state.Severity == "NORMAL") state.Severity = "WARNING";
            if (ratio >= thresholds.CriticalRatio && timestamp >= state.CooldownUntil)
            {
                state.CriticalSince ??= timestamp;
                if (timestamp - state.CriticalSince >= thresholds.CriticalHoldSeconds) state.Severity = "CRITICAL";
            }
            else state.CriticalSince = null;
            // Recovery requires sustained valid observations, then a fresh episode and cooldown.
            // Missing detections never count as recovery evidence.
            if (ratio <= thresholds.RecoveryRatio)
            {
                state.RecoverySince ??= timestamp;
                if (timestamp - state.RecoverySince >= thresholds.RecoveryHoldSeconds && state.Severity != "NORMAL")
                {
                    state.Severity = "NORMAL";
                    state.Episode = Guid.NewGuid().ToString("N");
                    state.CooldownUntil = timestamp + thresholds.CooldownSeconds;
                    state.CriticalSince = null;
                }
            }
            else state.RecoverySince = null;
            // Chỉ phát RiskDecision cho track đang WARNING hoặc CRITICAL.
            // Decision chứa TrackId, Episode, Severity, Ratio, Timestamp và Box.
            // NORMAL không tạo decision cảnh báo.
            if (state.Severity != "NORMAL") decisions.Add(new(detection.TrackId, state.Episode, state.Severity, ratio, timestamp, detection.Box));
        }
        return decisions;
    }
}
