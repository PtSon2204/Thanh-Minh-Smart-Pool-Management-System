using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed record PendingAlertSnapshot(Guid Id, RecordRiskCommand Command, int Attempts = 0,
    DateTimeOffset NextAttemptAt = default);
internal sealed record SnapshotFrame(RecordRiskCommand Command, byte[] Jpeg, byte[]? RenderedJpeg);

// Outbox trên đĩa: snapshot và sự kiện AI chờ upload, chưa phải bản ghi Warning.
// Inference chỉ stage byte ảnh; render/upload/persist chạy ở SnapshotUploadWorker.
public sealed class AlertSnapshotQueue(MonitoringFiles files, IOptions<MonitoringOptions> options, ILogger<AlertSnapshotQueue> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, long> staged = new();
    private DateTime nextFullWarning;
    private DateTime nextStagingWarning;
    private long rejected;
    public long RejectedSnapshots => Interlocked.Read(ref rejected);
    private readonly object stagingGate = new();
    private readonly Dictionary<string, long> queued = new();
    private readonly SemaphoreSlim ready = new(0, 3);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> priorities = new();
    private readonly Channel<SnapshotFrame> staging = Channel.CreateBounded<SnapshotFrame>(new BoundedChannelOptions(16) {
        FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    // Warnings cannot consume the RAM or disk reserve for LIVE escalations.
    private readonly Channel<SnapshotFrame> criticalStaging = Channel.CreateBounded<SnapshotFrame>(new BoundedChannelOptions(16) {
        FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    internal ChannelReader<SnapshotFrame> Staging => staging.Reader;
    internal ChannelReader<SnapshotFrame> CriticalStaging => criticalStaging.Reader;
    internal void CompleteStaging() { staging.Writer.TryComplete(); criticalStaging.Writer.TryComplete(); }
    internal Task<bool> WaitForWorkAsync(CancellationToken ct) => ready.WaitAsync(TimeSpan.FromMilliseconds(200), ct);
    private static bool ValidRendered(byte[] jpeg) => jpeg.Length is >= 4 and <= 4 * 1024 * 1024 && jpeg[0] == 0xff && jpeg[1] == 0xd8;

    // Hot path LIVE: no filesystem, no await, no image copies. Capture buffers are immutable.
    public bool TryStage(RecordRiskCommand command, byte[] jpeg, byte[]? renderedJpeg = null)
    {
        if (!ValidRendered(jpeg) || renderedJpeg != null && !ValidRendered(renderedJpeg)) return false;
        var key = $"{command.SourceType}:{command.SourceId}:{command.Decision.TrackId}:{command.Decision.Episode}:{command.Decision.Severity}";
        var slot = new DateTimeOffset(DateTime.SpecifyKind(command.ObservedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
        lock (stagingGate)
        {
            if (queued.TryGetValue(key, out var previous) && previous >= slot) return true;
            var writer = IsLiveCritical(command) ? criticalStaging.Writer : staging.Writer;
            if (!writer.TryWrite(new(command, jpeg, renderedJpeg)))
            {
                Interlocked.Increment(ref rejected);
                if (DateTime.UtcNow >= nextStagingWarning)
                {
                    logger.LogWarning("Snapshot RAM backlog full; {RejectedSnapshots} observations refused. Producer must retry; no imageless warning was inserted.", RejectedSnapshots);
                    nextStagingWarning = DateTime.UtcNow.AddSeconds(15);
                }
                return false;
            }
            queued[key] = slot;
            if (queued.Count > 8192) foreach (var old in queued.Keys.Take(4096).ToArray()) queued.Remove(old);
            return true;
        }
    }

    public async Task<bool> StageAsync(RecordRiskCommand command, byte[] jpeg, CancellationToken ct, byte[]? renderedJpeg = null)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xff || jpeg[1] != 0xd8) throw new ArgumentException("Snapshot phải là JPEG thực tế.");
        if (renderedJpeg != null && !ValidRendered(renderedJpeg)) throw new ArgumentException("Ảnh đã vẽ phải là JPEG hợp lệ.");
        var key = $"{command.SourceType}:{command.SourceId}:{command.Decision.TrackId}:{command.Decision.Episode}:{command.Decision.Severity}";
        var second = new DateTimeOffset(DateTime.SpecifyKind(command.ObservedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var slot = command.SourceType == "LIVE" ? second : 0;
        await gate.WaitAsync(ct);
        try
        {
            if (staged.TryGetValue(key, out var previous) && previous >= slot) return true;
            var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{key}:{slot}"))[..16]);
            var path = files.PathFor($"{id:N}-pending-alert.json");
            if (File.Exists(path)) { staged[key] = slot; return true; }
            var capacity = Math.Clamp(options.Value.MaxPendingAlertSnapshots, 16, 2048) + (IsLiveCritical(command) ? 16 : 0);
            if (Directory.EnumerateFiles(files.Root, "*-pending-alert.json").Take(capacity).Count() >= capacity)
            {
                if (DateTime.UtcNow >= nextFullWarning)
                { logger.LogWarning("Snapshot outbox is full; no alert without an image will be inserted."); nextFullWarning = DateTime.UtcNow.AddSeconds(15); }
                return false;
            }
            await File.WriteAllBytesAsync(files.PathFor($"{id:N}-source.jpg"), jpeg, ct);
            await File.WriteAllTextAsync(files.PathFor($"{id:N}-boxes.json"), JsonSerializer.Serialize(new[] { command.Decision }, Json), ct);
            if (renderedJpeg != null) await File.WriteAllBytesAsync(files.PathFor($"{id:N}.jpg"), renderedJpeg, ct);
            await SaveAsync(new(id, command with { AlertId = id, ImageUrl = null }), ct);
            try { ready.Release(); } catch (SemaphoreFullException) { }
            staged[key] = slot;
            // Giữ bộ nhớ hữu hạn cả khi nhiều job/track đã kết thúc.
            if (staged.Count > 8192)
                foreach (var old in staged.Keys.Take(4096).ToArray()) staged.Remove(old);
            return true;
        }
        finally { gate.Release(); }
    }

    private static bool IsLiveCritical(RecordRiskCommand command) => command.SourceType == "LIVE" && command.Decision.Severity == "CRITICAL";

    public IEnumerable<string> PendingFiles()
    {
        if (!Directory.Exists(files.Root)) return [];
        var paths = Directory.EnumerateFiles(files.Root, "*-pending-alert.json").ToArray();
        var existing = paths.ToHashSet(StringComparer.Ordinal);
        foreach (var old in priorities.Keys.Where(path => !existing.Contains(path))) priorities.TryRemove(old, out _);
        return paths.OrderBy(Priority).ThenByDescending(File.GetCreationTimeUtc).ToArray();
    }

    private static int Priority(PendingAlertSnapshot job) => (job.Command.SourceType == "LIVE" ? 0 : 2)
        + (job.Command.Decision.Severity == "CRITICAL" ? 0 : 1);
    private int Priority(string path) => priorities.GetOrAdd(path, file => {
        try { return Priority(JsonSerializer.Deserialize<PendingAlertSnapshot>(File.ReadAllText(file), Json)!); }
        catch { return 4; }
    });

    public async Task<PendingAlertSnapshot> ReadAsync(string path, CancellationToken ct)
    {
        await using var input = File.OpenRead(path);
        var job = await JsonSerializer.DeserializeAsync<PendingAlertSnapshot>(input, Json, ct) ?? throw new InvalidDataException("Invalid snapshot outbox item.");
        if (Path.GetFileName(path) != $"{job.Id:N}-pending-alert.json") throw new InvalidDataException("Snapshot outbox identity mismatch.");
        return job;
    }

    public async Task SaveAsync(PendingAlertSnapshot job, CancellationToken ct)
    {
        var temp = files.PathFor($"{job.Id:N}-pending-alert.tmp");
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(job, Json), ct);
        File.Move(temp, files.PathFor($"{job.Id:N}-pending-alert.json"), true);
        priorities[files.PathFor($"{job.Id:N}-pending-alert.json")] = Priority(job);
    }

    public void Complete(Guid id)
    {
        priorities.TryRemove(files.PathFor($"{id:N}-pending-alert.json"), out _);
        foreach (var suffix in new[] { ".jpg", "-source.jpg", "-boxes.json", "-pending-alert.json" }) File.Delete(files.PathFor($"{id:N}{suffix}"));
    }
}
