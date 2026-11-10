using System.Collections.Concurrent;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

/// <summary>
/// Giữ decisions (box cảnh báo) của từng frame trong lúc job video đang phân tích,
/// để video phát song song overlay đúng box của frame đó — khớp 1:1 với video đã vẽ khung
/// khi hoàn tất. Chỉ tồn tại trong bộ nhớ; tự dọn khi hết đời job.
/// </summary>
public sealed class VideoAnalysisState : IVideoFrameAnalysisReader
{
    private sealed class Entry
    {
        public double Fps;
        public RiskDecision[][] Frames = [];
        public int Ready; // Số frame đã phân tích xong (index 0..Ready-1).
        public DateTime UpdatedUtc = DateTime.UtcNow;
    }
    private readonly ConcurrentDictionary<Guid, Entry> entries = new();

    public void Prepare(Guid jobId, double fps, int frameCount)
    {
        Prune();
        // FrameCount null (container không khai báo số frame): dự phòng cho video tối đa 600s@60fps.
        var capacity = frameCount > 0 ? frameCount : 40000;
        entries[jobId] = new Entry { Fps = fps, Frames = new RiskDecision[capacity][] };
    }

    // Worker gọi tuần tự theo index; chỉ một thread ghi mỗi job nên không cần khóa.
    public void SetFrame(Guid jobId, int index, IReadOnlyList<RiskDecision> decisions)
    {
        if (!entries.TryGetValue(jobId, out var e) || index < 0 || index >= e.Frames.Length) return;
        e.Frames[index] = [.. decisions];
        e.UpdatedUtc = DateTime.UtcNow;
        Interlocked.Exchange(ref e.Ready, index + 1);
    }

    public void Remove(Guid jobId) => entries.TryRemove(jobId, out _);

    /// <summary>Decisions của đúng index frame (phase render lấy lại để vẽ khung). Rỗng khi chưa có.</summary>
    public IReadOnlyList<RiskDecision> FrameAt(Guid jobId, int index)
    {
        return entries.TryGetValue(jobId, out var e) && index >= 0 && index < e.Frames.Length && e.Frames[index] != null
            ? e.Frames[index] : [];
    }

    /// <summary>Trả decisions của frame gần nhất đã phân tích <= giây; null nếu chưa có frame nào.</summary>
    public (double? AnalyzedAt, IReadOnlyList<RiskDecision>? Decisions) Query(Guid jobId, double seconds)
    {
        if (!entries.TryGetValue(jobId, out var e) || e.Ready == 0 || e.Fps <= 0) return (null, null);
        var frame = Math.Clamp((int)(seconds * e.Fps), 0, e.Ready - 1);
        return (frame / e.Fps, e.Frames[frame]);
    }

    public Task<VideoFrameAnalysisDto> Query(Guid jobId, double seconds, CancellationToken ct)
    {
        var (analyzed, decisions) = Query(jobId, seconds);
        return Task.FromResult(new VideoFrameAnalysisDto(analyzed, decisions ?? []));
    }

    private void Prune()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        foreach (var (id, entry) in entries.Where(x => x.Value.UpdatedUtc < cutoff)) entries.TryRemove(id, out _);
    }
}
