namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed class RiskOptions
{
    private readonly object sync = new();
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

    public RiskOptions Clone()
    {
        lock (sync) return new()
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
    }

    // Ghi đè toàn bộ giá trị vào instance hiện tại. Provider dùng để "nóng" đổi ngưỡng
    // mà không tạo instance mới (giữ nguyên state theo dõi trong engine).
    public void CopyFrom(RiskOptions source)
    {
        var s = source.Clone();
        lock (sync)
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
}

public sealed record TrackedDetection(string TrackId, string Class, double Confidence, double[] Box);
public sealed record RiskDecision(string TrackId, string Episode, string Severity, double Ratio, double Timestamp, double[] Box);

// One engine per camera/video; timestamps are source timestamps, never inference duration.
public sealed class RiskDecisionEngine(RiskOptions options)
{
    // Mỗi TrackId có một State riêng để theo dõi lịch sử nguy cơ.
    // Window lưu kết quả normal/drowning theo timestamp.
    // Severity lưu mức nguy cơ hiện tại.
    // CriticalSince và RecoverySince dùng để tính thời gian duy trì điều kiện.
    // Episode phân biệt các đợt nguy cơ của cùng một đối tượng.
    // CooldownUntil ngăn tái kích hoạt ngay sau khi hồi phục.
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

    // Display continuity only: skipped low-confidence/missing observations do not
    // resolve a latched risk. These IDs must never be persisted as new observations.
    public IReadOnlyList<string> ActiveTrackIds() => states.Where(x => x.Value.Severity != "NORMAL").Select(x => x.Key).ToArray();

    public IReadOnlyList<RiskDecision> Observe(double timestamp, IEnumerable<TrackedDetection> detections)
    {
        if (!double.IsFinite(timestamp)) throw new ArgumentOutOfRangeException(nameof(timestamp));
        // One coherent threshold version for this entire observation, shared
        // safely with CopyFrom without resetting the track's temporal state.
        var thresholds = options.Clone();
        // Lấy bản sao ngưỡng tại đầu mỗi lần Observe,
        // tránh sử dụng lẫn ngưỡng cũ và ngưỡng mới trong cùng lượt xử lý.
        // Absence never emits NORMAL or resolves an alert.
        foreach (var key in states.Where(x => timestamp - x.Value.Last > 120).Select(x => x.Key).ToArray()) states.Remove(key);
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
                if (states.Count >= 4096)
                    foreach (var key in states.OrderBy(x => x.Value.Last).Take(1024).Select(x => x.Key).ToArray()) states.Remove(key);
                states[detection.TrackId] = state = new State();
            }
            if (timestamp <= state.Last) continue;
            if (timestamp - state.Last > thresholds.MaxObservationGapSeconds)
            {
                state.Window.Clear(); state.CriticalSince = null; state.RecoverySince = null;
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
            // NORMAL -> WARNING khi ratio >= 40% và đã hết cooldown.
            // WARNING/NORMAL -> CRITICAL chỉ khi ratio >= 70%
            // được duy trì đủ 3 giây theo timestamp observation.
            // Nếu ratio giảm dưới ngưỡng CRITICAL, reset timer hold,
            // nhưng không tự hạ Severity từ CRITICAL về WARNING.
            if (ratio >= thresholds.WarningRatio && timestamp >= state.CooldownUntil && state.Severity == "NORMAL") state.Severity = "WARNING";
            if (ratio >= thresholds.CriticalRatio && timestamp >= state.CooldownUntil)
            {
                state.CriticalSince ??= timestamp;
                if (timestamp - state.CriticalSince >= thresholds.CriticalHoldSeconds) state.Severity = "CRITICAL";
            }
            else state.CriticalSince = null;
            // Chỉ chuyển về NORMAL khi tỷ lệ drowning <= 20%
            // được duy trì đủ 5 giây với các observation hợp lệ.
            // Khi recovery: tạo Episode mới và đặt cooldown 15 giây.
            // Không dùng việc mất detection để xác nhận hồi phục.
            if (ratio <= thresholds.RecoveryRatio)
            {
                state.RecoverySince ??= timestamp;
                if (timestamp - state.RecoverySince >= thresholds.RecoveryHoldSeconds && state.Severity != "NORMAL")
                {
                    state.Severity = "NORMAL"; state.Episode = Guid.NewGuid().ToString("N");
                    state.CooldownUntil = timestamp + thresholds.CooldownSeconds; state.CriticalSince = null;
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
