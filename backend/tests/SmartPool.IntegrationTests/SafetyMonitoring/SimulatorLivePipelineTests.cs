using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.Persistence.DbContext;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace SmartPool.IntegrationTests.SafetyMonitoring;

public sealed partial class SafetyMonitoringTests
{
    // Opt-in: DB bị cô lập bởi fixture; không insert/call API để tạo cảnh báo giả.
    [SimulatorLiveFact]
    public async Task SimulatorRtsp_ExercisesExistingLivePipeline_WithRealModelAndDatabase()
    {
        var uri = new Uri(Environment.GetEnvironmentVariable("SMARTPOOL_SIMULATOR_TEST_URL")!);
        Assert.Equal("rtsp", uri.Scheme);
        Assert.True(uri.IsLoopback, "Acceptance test chỉ nhận MediaMTX loopback.");
        Assert.Empty(uri.UserInfo);
        await using var app = await ApiAsync(native: true, simulator: uri);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var token = await TokenAsync(app, "ADMIN");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
        await socket.ConnectAsync(new UriBuilder(client.BaseAddress!) { Scheme = "ws", Path = "/hubs/notifications" }.Uri, default);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, default);
        using var stopSocket = new CancellationTokenSource();
        var hubEvents = new ConcurrentQueue<string>();
        var receiving = Task.Run(async () => {
            var buffer = new byte[16384]; var pending = "";
            try {
                while (!stopSocket.IsCancellationRequested) {
                    var read = await socket.ReceiveAsync(buffer.AsMemory(), stopSocket.Token);
                    if (read.MessageType == WebSocketMessageType.Close) break;
                    pending += Encoding.UTF8.GetString(buffer, 0, read.Count);
                    int end;
                    while ((end = pending.IndexOf('\u001e')) >= 0) {
                        var message = pending[..end]; pending = pending[(end + 1)..];
                        using var json = JsonDocument.Parse(message);
                        if (json.RootElement.TryGetProperty("target", out var target)) hubEvents.Enqueue(target.GetString()!);
                    }
                }
            } catch (OperationCanceledException) when (stopSocket.IsCancellationRequested) { }
        });
        try
        {
            var response = await client.PostAsJsonAsync("/api/equipments", new {
                name = "RTSP Simulator Acceptance", category = "Camera", location = "Simulator Test Pool",
                cameraConnection = new { protocol = "RTSP", streamUrl = uri.AbsoluteUri }
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var id = created.RootElement.GetProperty("id").GetGuid();
            Assert.NotEqual(Guid.Empty, id);
            // Lưu lại đúng ngưỡng hiệu lực qua API, không hạ ngưỡng để ép model phát cảnh báo.
            var thresholds = await client.GetFromJsonAsync<SafetyThresholdsDto>("/api/thresholds/safety-monitoring");
            Assert.NotNull(thresholds);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/thresholds/safety-monitoring", thresholds)).StatusCode);
            Assert.Equal("DATABASE", (await client.GetFromJsonAsync<SafetyThresholdsDto>("/api/thresholds/safety-monitoring"))!.Origin);
            Guid? secondId = null;
            if (Environment.GetEnvironmentVariable("SMARTPOOL_SIMULATOR_TEST_SECOND_URL") is { Length: > 0 } secondUrl)
            {
                var secondUri = new Uri(secondUrl);
                Assert.True(secondUri.IsLoopback); Assert.Equal("rtsp", secondUri.Scheme); Assert.Empty(secondUri.UserInfo);
                Assert.Equal(uri.DnsSafeHost, secondUri.DnsSafeHost); Assert.Equal(uri.Port, secondUri.Port);
                Assert.NotEqual(uri.AbsolutePath, secondUri.AbsolutePath);
                var secondResponse = await client.PostAsJsonAsync("/api/equipments", new {
                    name = "RTSP Simulator Other Camera", category = "Camera", location = "Simulator Other Pool",
                    cameraConnection = new { protocol = "RTSP", streamUrl = secondUri.AbsoluteUri }
                });
                Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
                using var secondCreated = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
                secondId = secondCreated.RootElement.GetProperty("id").GetGuid();
                Assert.NotEqual(id, secondId);
            }

            // WebRTC nhận H.264 thật từ đúng camera, cùng service mà endpoint views sử dụng.
            using var peer = new RTCPeerConnection(new RTCConfiguration { X_ICEIncludeAllInterfaceAddresses = true, X_GatherTimeoutMs = 10000 });
            peer.addTrack(new MediaStreamTrack(new VideoFormat(VideoCodecsEnum.H264, 96, 90000,
                "packetization-mode=1;profile-level-id=42e01f;level-asymmetry-allowed=1"), MediaStreamStatusEnum.RecvOnly));
            var videoFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            peer.OnVideoFrameReceived += (_, _, bytes, _) => { if (bytes.Length > 0) videoFrame.TrySetResult(); };
            var offer = peer.createOffer(new RTCOfferOptions { X_WaitForIceGatheringToComplete = true });
            await peer.setLocalDescription(offer);
            var streams = app.Services.GetRequiredService<CameraStreamService>();
            var view = await streams.OpenAsync(id, "simulator-acceptance", new CameraOffer("offer", offer.sdp), default);
            Assert.Equal(SetDescriptionResultEnum.OK, peer.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = view.Sdp }));
            await videoFrame.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await streams.CloseAsync(id, view.ViewId, "simulator-acceptance");

            var frames = new HashSet<string>(); var tracks = new HashSet<string>(); var sessions = new HashSet<string>();
            var latencies = new List<double>(); var inference = new List<double>();
            var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
            while (DateTimeOffset.UtcNow < deadline)
            {
                var session = app.Services.GetRequiredService<CameraService>().GetStatus(id).AnalysisSession;
                if (session != null) sessions.Add(session);
                using var analysis = JsonDocument.Parse(await client.GetStringAsync($"/api/cameras/{id}/analysis"));
                var root = analysis.RootElement;
                if (root.GetProperty("state").GetString() == "Connected" && root.TryGetProperty("frameId", out var frame) && frames.Add(frame.GetString()!))
                {
                    if (root.TryGetProperty("inferenceMilliseconds", out var ms) && ms.ValueKind == JsonValueKind.Number) inference.Add(ms.GetDouble());
                    if (root.TryGetProperty("observedAt", out var observed)) latencies.Add((DateTimeOffset.UtcNow - observed.GetDateTimeOffset()).TotalMilliseconds);
                    if (root.TryGetProperty("detections", out var detections) && detections.ValueKind == JsonValueKind.Array)
                        foreach (var detection in detections.EnumerateArray()) tracks.Add(detection.GetProperty("trackId").GetString()!);
                }
                await Task.Delay(100);
            }
            Assert.True(frames.Count >= 3, $"Chỉ nhận {frames.Count} frame AI thực tế.");
            await using var db = new SmartPoolDbContext(options);
            Assert.Empty(await db.VideoAnalysisJobs.ToListAsync());
            var alerts = await db.Warnings.AsNoTracking().Where(x => x.CamId == id).ToListAsync();
            Assert.All(alerts, alert => Assert.Equal("LIVE", alert.SourceType));
            if (alerts.Count > 0) Assert.Contains("alert_created", hubEvents);
            var critical = alerts.FirstOrDefault(x => x.SeverityLevel == "CRITICAL" && x.Status == "PENDING");
            if (critical != null)
            {
                var ack = await client.PatchAsJsonAsync($"/api/alerts/{critical.Id}/acknowledge", new { expectedSeverity = critical.SeverityLevel });
                Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
                Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/alerts/{critical.Id}/acknowledge", new { expectedSeverity = critical.SeverityLevel })).StatusCode);
                Assert.Equal(1, await db.Incidents.CountAsync(x => x.WarningId == critical.Id && x.CameraId == id));
            }
            var warning = alerts.FirstOrDefault(x => x.SeverityLevel == "WARNING" && x.Status == "PENDING");
            Guid? cancelId = warning?.Id;
            if (secondId != null)
            {
                using var secondAnalysis = JsonDocument.Parse(await client.GetStringAsync($"/api/cameras/{secondId}/analysis"));
                Assert.Equal(secondId, secondAnalysis.RootElement.GetProperty("cameraId").GetGuid());
                cancelId ??= await db.Warnings.AsNoTracking().Where(x => x.CamId == secondId && x.Status == "PENDING").Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
            }
            if (cancelId != null)
            {
                var cancelAlert = await client.GetFromJsonAsync<AlertDto>($"/api/alerts/{cancelId}");
                Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/alerts/{cancelId}/cancel", new { expectedSeverity = cancelAlert!.Severity })).StatusCode);
                Assert.False(await db.Incidents.AnyAsync(x => x.WarningId == cancelId));
            }
            var report = new { cameraId = id, secondCameraId = secondId, analysisSessionsObserved = sessions.Count,
                thresholdSource = "DATABASE", aiFramesObserved = frames.Count, tracksObserved = tracks.Count,
                observedFramesPerSecond = frames.Count / 25.0, meanInferenceMs = inference.Count == 0 ? (double?)null : inference.Average(),
                maxObservationAgeMs = latencies.Count == 0 ? (double?)null : latencies.Max(),
                alerts = alerts.Select(x => new { x.Id, x.SourceType, x.SeverityLevel, x.SnapshotStatus }),
                criticalAcknowledgementTested = critical != null, cancellationTested = cancelId != null,
                signalrEvents = hubEvents.ToArray(), cloudinarySuccessTested = false, browserPopupAndTtsTested = false };
            var reportPath = Path.Combine(RepositoryRoot(), "tools", "camera-simulator", ".runtime", "live-acceptance.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { stopSocket.Cancel(); await receiving; await app.StopAsync(); }
    }
}

public sealed class SimulatorLiveFactAttribute : FactAttribute
{
    public SimulatorLiveFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPOOL_SIMULATOR_TEST_URL")) ||
            !File.Exists(Environment.GetEnvironmentVariable("SMARTPOOL_SAFETY_TEST_PYTHON")) ||
            !File.Exists(Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")))
            Skip = "Requires an explicitly started local RTSP simulator, local Python/ONNX and FFmpeg.";
    }
}
