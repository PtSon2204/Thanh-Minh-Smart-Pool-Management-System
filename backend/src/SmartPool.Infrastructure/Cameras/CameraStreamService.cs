using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SIPSorcery.Net;
using SIPSorcery.Sys;
using SIPSorceryMedia.Abstractions;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraStreamService(CameraService cameras, IOptions<CameraOptions> options, CameraAnalysisState? analysis = null) : BackgroundService, ICameraStreamService
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly PortRange ports = new(options.Value.WebRtc.PortRangeStart, options.Value.WebRtc.PortRangeEnd);
    private readonly Dictionary<(Guid CameraId, bool Analysis), IWebRtcVideoSource> sources = [];
    private readonly Dictionary<Guid, Viewer> viewers = [];

    private sealed class Viewer(Guid equipmentId, string owner, IWebRtcVideoSource source, RTCPeerConnection peer, bool annotated)
    {
        public Guid EquipmentId { get; } = equipmentId;
        public string Owner { get; } = owner;
        public IWebRtcVideoSource Source { get; } = source;
        public bool Annotated { get; } = annotated;
        public RTCPeerConnection Peer { get; } = peer;
        public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? SourceMissingAt { get; set; }
        private bool awaitingKey = true;
        private readonly object sendGate = new();
        public void Send(byte[] bytes, bool key, uint duration, uint? captureTimestamp)
        {
            lock (sendGate)
            {
                if (Peer.connectionState != RTCPeerConnectionState.connected) { awaitingKey = true; return; }
                if (awaitingKey && !key) return;
                awaitingKey = false;
                try {
                    if (captureTimestamp.HasValue) SendCapturedFrame(bytes, captureTimestamp.Value);
                    else Peer.SendVideo(duration, bytes);
                }
                catch (Exception) { try { Peer.Close("Video send failed"); } catch (Exception) { } }
            }
        }
        // Keep the original presentation clock on every RTP packet, including
        // fragmented NALs. SendRtpRaw still handles sequence numbers, SRTP and RTCP.
        private void SendCapturedFrame(byte[] bytes, uint timestamp)
        {
            var format = Peer.VideoLocalTrack.Capabilities.First().ToVideoFormat();
            const int maxPayload = 1200;
            foreach (var nal in H264Packetiser.ParseNals(bytes))
            {
                if (nal.NAL.Length <= maxPayload)
                {
                    Peer.SendRtpRaw(SDPMediaTypesEnum.video, nal.NAL, timestamp, nal.IsLast ? 1 : 0, format.FormatID);
                    continue;
                }
                for (var offset = 1; offset < nal.NAL.Length; offset += maxPayload - 2)
                {
                    var length = Math.Min(maxPayload - 2, nal.NAL.Length - offset);
                    var last = offset + length == nal.NAL.Length;
                    var header = H264Packetiser.GetH264RtpHeader(nal.NAL[0], offset == 1, last);
                    var payload = new byte[length + header.Length];
                    header.CopyTo(payload, 0);
                    nal.NAL.AsSpan(offset, length).CopyTo(payload.AsSpan(header.Length));
                    Peer.SendRtpRaw(SDPMediaTypesEnum.video, payload, timestamp, last && nal.IsLast ? 1 : 0, format.FormatID);
                }
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
            if (offer.Analysis && (analysis == null || analysis.Get(equipmentId).State == "NotConfigured"))
                throw new CameraConnectionException("NotConfigured", "AI chưa được cấu hình để phát hình và box đồng bộ.");
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
                // Include all interfaces even when BindAddress is set so Linux loopback ICE candidates are discovered. //fix ci sửa ngày 11/10/2026
                X_ICEIncludeAllInterfaceAddresses = true, X_GatherTimeoutMs = 10000,
                iceServers = settings.IceServers.Select(x => new RTCIceServer { urls = x.Urls, username = x.Username, credential = x.Credential }).ToList()
            }, portRange: ports);
            try
            {
                peer.addTrack(new MediaStreamTrack(new VideoFormat(VideoCodecsEnum.H264, 96, 90000,
                    "packetization-mode=1;profile-level-id=42e01f;level-asymmetry-allowed=1"), MediaStreamStatusEnum.SendOnly));
                if (peer.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = offer.Sdp }) != SetDescriptionResultEnum.OK)
                    throw new CameraConnectionException("InvalidOffer", "Không thể kết nối đến camera");
                var paired = offer.Analysis && (!offer.Smooth || !cameras.GetStatus(equipmentId).SmoothPlayback);
                var sourceKey = (equipmentId, paired);
                if (!sources.TryGetValue(sourceKey, out var source))
                {
                    source = paired ? new CameraAnalysisVideoSource(analysis!, equipmentId, options.Value)
                        : new CameraWebRtcSource(cameras, equipmentId, options.Value);
                    sources.Add(sourceKey, source);
                }
                viewer = new Viewer(equipmentId, owner, source, peer, paired);
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
            if (viewer.Source.Failed || SourceUnavailable(viewer)) throw Unavailable();
            var state = viewer.Peer.connectionState switch
            {
                RTCPeerConnectionState.connected => "Connected",
                RTCPeerConnectionState.failed or RTCPeerConnectionState.closed or RTCPeerConnectionState.disconnected => "Disconnected",
                _ => "Connecting"
            };
            if (state == "Connected" && cameras.GetStatus(equipmentId).State != "Connected") state = "Connecting";
            return new CameraViewStatus(state, "H264", viewer.Source.Transcoding);
        }
        finally { gate.Release(); }
    }

    public async Task CloseAsync(Guid equipmentId, Guid viewId, string owner)
    {
        IWebRtcVideoSource? unused = null;
        Viewer? closed = null;
        await gate.WaitAsync();
        try
        {
            if (!viewers.TryGetValue(viewId, out var viewer) || viewer.EquipmentId != equipmentId || viewer.Owner != owner) return;
            viewers.Remove(viewId);
            closed = viewer;
            if (!viewers.Values.Any(x => x.EquipmentId == equipmentId && x.Annotated == viewer.Annotated))
                sources.Remove((equipmentId, viewer.Annotated), out unused);
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
                        || x.Value.Source.Failed || SourceUnavailable(x.Value)
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
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        shutdown.CancelAfter(TimeSpan.FromSeconds(5));
        KeyValuePair<Guid, Viewer>[] active;
        try { await gate.WaitAsync(shutdown.Token); }
        catch (OperationCanceledException) { return; }
        try { active = viewers.ToArray(); }
        finally { gate.Release(); }
        foreach (var pair in active)
        {
            try { await CloseAsync(pair.Value.EquipmentId, pair.Key, pair.Value.Owner).WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception) { }
        }
    }

    private static CameraConnectionException Unavailable() => new("StreamUnavailable", "Camera hiện không khả dụng");

    // Giữ ICE/decoder trong khoảng ngắt ngắn ở seam video simulator. Không phát frame cũ;
    // player vẫn ẩn hình khi thiếu frame, và camera vật lý giữ quy tắc ngắt ngay như trước.
    private bool SourceUnavailable(Viewer viewer)
    {
        var status = cameras.GetStatus(viewer.EquipmentId);
        if (status.State == "Connected") { viewer.SourceMissingAt = null; return false; }
        if (status.AnalysisSession == null) return true;
        viewer.SourceMissingAt ??= DateTimeOffset.UtcNow;
        return DateTimeOffset.UtcNow - viewer.SourceMissingAt > TimeSpan.FromSeconds(5);
    }
}
