using System.Collections.Concurrent;
using System.Text.Json;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

/// <summary>
/// Giữ decisions (box cảnh báo) của từng frame trong lúc job video đang phân tích,
/// để video phát song song overlay đúng box của frame đó — khớp 1:1 với video đã vẽ khung
/// khi hoàn tất. Timeline hoàn chỉnh được lưu riêng trên ổ đĩa để xem lại sau render/restart.
/// </summary>
public sealed class VideoAnalysisState(MonitoringFiles files) : IVideoFrameAnalysisReader
{
    private sealed class Entry
    {
        public double Fps;
        public RiskDecision[][] Frames = [];
        public int Ready; // Số frame đã phân tích xong (index 0..Ready-1).
        public bool Complete;
        public bool Pinned;
        public DateTime UpdatedUtc = DateTime.UtcNow;
    }
    private readonly ConcurrentDictionary<Guid, Entry> entries = new();

    public void Prepare(Guid jobId, double fps, int frameCount)
    {
        Prune();
        // FrameCount null (container không khai báo số frame): dự phòng cho video tối đa 600s@60fps.
        var capacity = frameCount > 0 ? frameCount : 40000;
        entries[jobId] = new Entry { Fps = fps, Frames = new RiskDecision[capacity][], Pinned = true };
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

    private sealed record StoredTimeline(double Fps, RiskDecision[][] Frames);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task CompleteAsync(Guid jobId, CancellationToken ct)
    {
        if (!entries.TryGetValue(jobId, out var entry)) return;
        var ready = Volatile.Read(ref entry.Ready);
        var path = files.PathFor($"{jobId:N}-timeline.json");
        var temp = files.PathFor($"{jobId:N}-timeline.tmp");
        await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            await JsonSerializer.SerializeAsync(output, new StoredTimeline(entry.Fps, entry.Frames[..ready]), Json, ct);
        File.Move(temp, path, true);
        entry.Complete = true;
    }

    public async Task<VideoFrameAnalysisWindowDto> WindowAsync(Guid jobId, double from, double duration, CancellationToken ct)
    {
        if (!entries.TryGetValue(jobId, out var entry))
        {
            var path = files.PathFor($"{jobId:N}-timeline.json");
            if (!File.Exists(path)) return new(0, 0, 0, false, []);
            Prune();
            await using var input = File.OpenRead(path);
            var stored = await JsonSerializer.DeserializeAsync<StoredTimeline>(input, Json, ct) ?? throw new InvalidDataException("Invalid video timeline.");
            if (!double.IsFinite(stored.Fps) || stored.Fps is < 1 or > 60 || stored.Frames.Length > 40000)
                throw new InvalidDataException("Invalid video timeline.");
            entry = entries.GetOrAdd(jobId, new Entry { Fps = stored.Fps, Frames = stored.Frames, Ready = stored.Frames.Length, Complete = true });
        }
        entry.UpdatedUtc = DateTime.UtcNow;
        var ready = Volatile.Read(ref entry.Ready);
        var start = (int)Math.Min(int.MaxValue, Math.Floor(from * entry.Fps));
        var count = Math.Min((int)Math.Ceiling(duration * entry.Fps), Math.Max(0, ready - start));
        var frames = new RiskDecision[count][];
        for (var index = 0; index < count; index++) frames[index] = entry.Frames[start + index] ?? [];
        return new(entry.Fps, start, ready, entry.Complete, frames);
    }

    /// <summary>Decisions của đúng index frame (phase render lấy lại để vẽ khung). Rỗng khi chưa có.</summary>
    public IReadOnlyList<RiskDecision> FrameAt(Guid jobId, int index)
    {
        if (!entries.TryGetValue(jobId, out var e) || index < 0 || index >= e.Frames.Length) return [];
        e.UpdatedUtc = DateTime.UtcNow;
        return e.Frames[index] ?? [];
    }

    /// <summary>Trả decisions của frame gần nhất đã phân tích <= giây; null nếu chưa có frame nào.</summary>
    public (double? AnalyzedAt, IReadOnlyList<RiskDecision>? Decisions) Query(Guid jobId, double seconds)
    {
        if (!entries.TryGetValue(jobId, out var e) || e.Ready == 0 || e.Fps <= 0) return (null, null);
        if (seconds < 0 || seconds * e.Fps >= Volatile.Read(ref e.Ready)) return (null, null);
        var frame = (int)(seconds * e.Fps);
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
        foreach (var (id, entry) in entries.Where(x => !x.Value.Pinned && x.Value.UpdatedUtc < cutoff)) entries.TryRemove(id, out _);
        foreach (var key in entries.Where(x => !x.Value.Pinned).OrderByDescending(x => x.Value.UpdatedUtc).Skip(8).Select(x => x.Key))
            entries.TryRemove(key, out _);
    }
}
