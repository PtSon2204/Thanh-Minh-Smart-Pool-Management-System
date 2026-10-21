using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SIPSorcery.Net;
using SIPSorcery.Sys;
using SIPSorceryMedia.Abstractions;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraStreamService(CameraService cameras, IOptions<CameraOptions> options) : BackgroundService, ICameraStreamService
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly PortRange ports = new(options.Value.WebRtc.PortRangeStart, options.Value.WebRtc.PortRangeEnd);
    private readonly Dictionary<Guid, CameraWebRtcSource> sources = [];
    private readonly Dictionary<Guid, Viewer> viewers = [];

    private sealed class Viewer(Guid equipmentId, string owner, CameraWebRtcSource source, RTCPeerConnection peer)
    {
        public Guid EquipmentId { get; } = equipmentId;
        public string Owner { get; } = owner;
        public CameraWebRtcSource Source { get; } = source;
        public RTCPeerConnection Peer { get; } = peer;
        public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
        private bool awaitingKey = true;
        private readonly object sendGate = new();
        public void Send(byte[] bytes, bool key, uint duration)
        {
            lock (sendGate)
            {
                if (Peer.connectionState != RTCPeerConnectionState.connected) { awaitingKey = true; return; }
                if (awaitingKey && !key) return;
                awaitingKey = false;
                try { Peer.SendVideo(duration, bytes); }
                catch (Exception) { try { Peer.Close("Video send failed"); } catch (Exception) { } }
            }
        }
        public void Close()
        {
            Source.Frame -= Send;
            lock (sendGate) { Peer.Close("Viewer closed"); Peer.Dispose(); }
        }
    }

    public CameraWebRtcConfiguration GetConfiguration() => new(options.Value.WebRtc.IceServers);

    public async Task<CameraView> OpenAsync(Guid equipmentId, string owner, CameraOffer offer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner) || offer.Type != "offer" || string.IsNullOrWhiteSpace(offer.Sdp) || offer.Sdp.Length > 65536)
            throw new CameraConnectionException("InvalidOffer", "Thông tin khởi tạo WebRTC không hợp lệ.");
        var id = Guid.NewGuid();
        Viewer? viewer = null;
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cameras.GetStatus(equipmentId).State != "Connected") throw Unavailable();
            if (viewers.Count >= 64) throw new CameraConnectionException("Capacity", "Đã đạt giới hạn phiên xem camera.");
            try
            {
                var description = SDP.ParseSDPDescription(offer.Sdp);
                if (description.Media.Count != 1 || description.Media[0].Media != SDPMediaTypesEnum.video)
                    throw new FormatException();
            }
            catch { throw new CameraConnectionException("InvalidOffer", "Thông tin khởi tạo WebRTC không hợp lệ."); }
            var settings = options.Value.WebRtc;
            var peer = new RTCPeerConnection(new RTCConfiguration
            {
                X_BindAddress = string.IsNullOrWhiteSpace(settings.BindAddress) ? null : IPAddress.Parse(settings.BindAddress),
                X_ICEIncludeAllInterfaceAddresses = true, X_GatherTimeoutMs = 8000,
                iceServers = settings.IceServers.Select(x => new RTCIceServer { urls = x.Urls, username = x.Username, credential = x.Credential }).ToList()
            }, portRange: ports);
            try
            {
                peer.addTrack(new MediaStreamTrack(new VideoFormat(VideoCodecsEnum.H264, 96, 90000,
                    "packetization-mode=1;profile-level-id=42e01f;level-asymmetry-allowed=1"), MediaStreamStatusEnum.SendOnly));
                if (peer.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = offer.Sdp }) != SetDescriptionResultEnum.OK)
                    throw new CameraConnectionException("InvalidOffer", "Không thể kết nối đến camera");
                if (!sources.TryGetValue(equipmentId, out var source))
                {
                    source = new CameraWebRtcSource(cameras, equipmentId, options.Value);
                    sources.Add(equipmentId, source);
                }
                viewer = new Viewer(equipmentId, owner, source, peer);
                source.Frame += viewer.Send;
                viewers.Add(id, viewer);
            }
            catch (CameraConnectionException) { peer.Close("Negotiation failed"); peer.Dispose(); throw; }
            catch { peer.Close("Negotiation failed"); peer.Dispose(); throw new CameraConnectionException("InvalidOffer", "Không thể kết nối đến camera"); }
        }
        finally { gate.Release(); }
        try
        {
            var gathered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            viewer.Peer.onicegatheringstatechange += state => { if (state == RTCIceGatheringState.complete) gathered.TrySetResult(); };
            if (viewer.Peer.iceGatheringState == RTCIceGatheringState.complete) gathered.TrySetResult();
            await gathered.Task.WaitAsync(TimeSpan.FromSeconds(12), cancellationToken);
            var answer = viewer.Peer.createAnswer();
            await viewer.Peer.setLocalDescription(answer);
            cancellationToken.ThrowIfCancellationRequested();
            return new CameraView(id, "answer", answer.sdp);
        }
        catch
        {
            await CloseAsync(equipmentId, id, owner);
            if (cancellationToken.IsCancellationRequested) throw;
            throw new CameraConnectionException("NegotiationFailed", "Không thể kết nối đến camera");
        }
    }

    public async Task<CameraViewStatus> GetStatusAsync(Guid equipmentId, Guid viewId, string owner, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!viewers.TryGetValue(viewId, out var viewer) || viewer.EquipmentId != equipmentId || viewer.Owner != owner
                || DateTimeOffset.UtcNow - viewer.LastSeen > TimeSpan.FromSeconds(35))
                throw new CameraConnectionException("ViewEnded", "Phiên xem camera đã kết thúc.");
            viewer.LastSeen = DateTimeOffset.UtcNow;
            if (viewer.Source.Failed || cameras.GetStatus(equipmentId).State != "Connected") throw Unavailable();
            var state = viewer.Peer.connectionState switch
            {
                RTCPeerConnectionState.connected => "Connected",
                RTCPeerConnectionState.failed or RTCPeerConnectionState.closed or RTCPeerConnectionState.disconnected => "Disconnected",
                _ => "Connecting"
            };
            return new CameraViewStatus(state, "H264", viewer.Source.Transcoding);
        }
        finally { gate.Release(); }
    }

    public async Task CloseAsync(Guid equipmentId, Guid viewId, string owner)
    {
        CameraWebRtcSource? unused = null;
        Viewer? closed = null;
        await gate.WaitAsync();
        try
        {
            if (!viewers.TryGetValue(viewId, out var viewer) || viewer.EquipmentId != equipmentId || viewer.Owner != owner) return;
            viewers.Remove(viewId);
            closed = viewer;
            if (!viewers.Values.Any(x => x.EquipmentId == equipmentId)) sources.Remove(equipmentId, out unused);
        }
        finally { gate.Release(); }
        closed.Close();
        if (unused != null) await unused.DisposeAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                KeyValuePair<Guid, Viewer>[] expired;
                await gate.WaitAsync(stoppingToken);
                try
                {
                    expired = viewers.Where(x => DateTimeOffset.UtcNow - x.Value.LastSeen > TimeSpan.FromSeconds(35)
                        || x.Value.Source.Failed || cameras.GetStatus(x.Value.EquipmentId).State != "Connected"
                        || x.Value.Peer.connectionState is RTCPeerConnectionState.failed or RTCPeerConnectionState.closed).ToArray();
                }
                finally { gate.Release(); }
                foreach (var pair in expired) await CloseAsync(pair.Value.EquipmentId, pair.Key, pair.Value.Owner);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        KeyValuePair<Guid, Viewer>[] active;
        await gate.WaitAsync();
        try { active = viewers.ToArray(); }
        finally { gate.Release(); }
        foreach (var pair in active) await CloseAsync(pair.Value.EquipmentId, pair.Key, pair.Value.Owner);
    }

    private static CameraConnectionException Unavailable() => new("StreamUnavailable", "Camera hiện không khả dụng");
}
