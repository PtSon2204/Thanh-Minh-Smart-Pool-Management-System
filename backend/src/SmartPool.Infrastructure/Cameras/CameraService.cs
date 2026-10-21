using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.ManageEquipments;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraService(CameraAddressPolicy policy, ICameraFrameDecoder decoder, IDataProtectionProvider protection,
    IOptions<CameraOptions> options, TimeProvider timeProvider, ILogger<CameraService> logger) : ICameraService, IAsyncDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, CameraWorker> workers = [];
    private readonly Dictionary<Guid, CameraWorker> published = [];
    private readonly IDataProtector protector = protection.CreateProtector("SmartPool.CameraConfiguration.v1");

    public async Task<string> GetIdentityAsync(CameraConnectionConfiguration configuration, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.ConnectionTimeoutSeconds, 2, 60)));
        try { return (await policy.ValidateAsync(configuration, timeout.Token)).Identity; }
        catch (CameraConnectionException) { throw; }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CameraConnectionException("StreamUnavailable", "Không thể kết nối đến camera. Vui lòng kiểm tra cấu hình kết nối.");
        }
    }

    public async Task<ICameraRegistration> PrepareAsync(CameraConnectionConfiguration configuration, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.ConnectionTimeoutSeconds, 2, 60)));
        CameraWorker? worker = null;
        string? identity = null;
        try
        {
            var target = await policy.ValidateAsync(configuration, timeout.Token);
            if (string.IsNullOrWhiteSpace(options.Value.FfmpegPath) || !Path.IsPathFullyQualified(options.Value.FfmpegPath) || !File.Exists(options.Value.FfmpegPath))
                throw new CameraConnectionException("NotConfigured", "Chưa cấu hình FFmpeg cho camera.");
            identity = target.Identity;
            var encrypted = protector.Protect(JsonSerializer.Serialize(configuration));
            lock (gate)
            {
                if (workers.ContainsKey(identity)) throw new CameraConnectionException("Duplicate", "Camera này đã được đăng ký hoặc đang kết nối.");
                if (workers.Count >= Math.Clamp(options.Value.MaxConnections, 1, 128)) throw new CameraConnectionException("Capacity", "Đã đạt giới hạn kết nối camera.");
                worker = new CameraWorker(configuration, target, decoder, options.Value, timeProvider, logger);
                workers.Add(identity, worker);
            }
            var failure = await worker.FirstFrame.Task.WaitAsync(timeout.Token);
            if (failure != null) throw failure;
            return new Registration(this, worker, identity, encrypted);
        }
        catch (Exception exception)
        {
            if (worker != null && identity != null) await RemoveAsync(identity, worker);
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            if (exception is CameraConnectionException) throw;
            throw new CameraConnectionException(exception is OperationCanceledException ? "Timeout" : "StreamUnavailable",
                "Không thể kết nối đến camera. Vui lòng kiểm tra cấu hình kết nối.");
        }
    }

    public CameraConnectionStatus GetStatus(Guid equipmentId)
    {
        lock (gate) return published.TryGetValue(equipmentId, out var worker) ? worker.GetStatus() : new("Disconnected", null);
    }

    public byte[]? GetFrame(Guid equipmentId) { lock (gate) return published.TryGetValue(equipmentId, out var worker) ? worker.GetFrame() : null; }

    internal IDisposable Subscribe(Guid equipmentId, Action<CameraEncodedFrame> receive)
    {
        lock (gate)
        {
            if (!published.TryGetValue(equipmentId, out var worker) || worker.GetStatus().State != "Connected")
                throw new CameraConnectionException("StreamUnavailable", "Camera hiện không khả dụng");
            return worker.Subscribe(receive);
        }
    }

    public async Task RestoreAsync(Guid equipmentId, string protectedConfiguration, CancellationToken cancellationToken)
    {
        lock (gate) if (published.ContainsKey(equipmentId)) return;
        var configuration = JsonSerializer.Deserialize<CameraConnectionConfiguration>(protector.Unprotect(protectedConfiguration))!;
        var target = await policy.ValidateAsync(configuration, cancellationToken);
        lock (gate)
        {
            if (published.ContainsKey(equipmentId) || workers.ContainsKey(target.Identity) || workers.Count >= Math.Clamp(options.Value.MaxConnections, 1, 128)) return;
            var worker = new CameraWorker(configuration, target, decoder, options.Value, timeProvider, logger) { EquipmentId = equipmentId, Registered = true };
            workers.Add(target.Identity, worker);
            published.Add(equipmentId, worker);
        }
    }

    public async Task ReconcileAsync(HashSet<Guid> equipmentIds)
    {
        KeyValuePair<string, CameraWorker>[] removed;
        lock (gate) removed = workers.Where(x => x.Value.Registered && x.Value.EquipmentId is Guid id && !equipmentIds.Contains(id)).ToArray();
        foreach (var pair in removed) await RemoveAsync(pair.Key, pair.Value);
    }

    private async Task RemoveAsync(string identity, CameraWorker worker)
    {
        lock (gate)
        {
            if (workers.TryGetValue(identity, out var current) && ReferenceEquals(current, worker)) workers.Remove(identity);
            if (worker.EquipmentId is Guid id && published.TryGetValue(id, out current) && ReferenceEquals(current, worker)) published.Remove(id);
        }
        await worker.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        KeyValuePair<string, CameraWorker>[] all;
        lock (gate) all = workers.ToArray();
        foreach (var pair in all) await RemoveAsync(pair.Key, pair.Value);
    }

    private sealed class Registration(CameraService service, CameraWorker worker, string identity, string encrypted) : ICameraRegistration
    {
        private bool confirmed;
        public string Identity => identity;
        public string ProtectedConfiguration => encrypted;
        public void Publish(Guid equipmentId)
        {
            if (worker.GetStatus().State != "Connected") throw new CameraConnectionException("StreamUnavailable", "Camera mất kết nối trước khi hoàn tất đăng ký.");
            lock (service.gate) { worker.EquipmentId = equipmentId; service.published.Add(equipmentId, worker); }
        }
        public void Confirm() { worker.Registered = true; confirmed = true; }
        public async ValueTask DisposeAsync() { if (!confirmed) await service.RemoveAsync(identity, worker); }
    }
}
