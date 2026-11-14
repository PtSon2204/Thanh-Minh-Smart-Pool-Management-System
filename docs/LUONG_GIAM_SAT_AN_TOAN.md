# Luồng chạy giám sát an toàn Smart Pool — hướng dẫn đọc code

Đối chiếu mã nguồn ngày **14/11/2026**. Tài liệu mô tả **code hiện có**, không coi các đề xuất sửa box trong cuộc trao đổi là tính năng đã triển khai.

Mục tiêu: đọc xong có thể trả lời “ai gọi hàm này?”, “hàm chạy lúc nào?”, “dữ liệu đi đâu?” và “vì sao video, box, popup, Incident không xuất hiện cùng lúc?”.

**Phân tầng sau khi chỉnh kiến trúc:** `SafetyMonitoringService` ở Application quyết định vòng đời Warning, chống tạo trùng theo episode, nâng mức, cooldown, xác nhận/hủy và điều kiện tạo Incident. `MonitoringRepository` ở Infrastructure chỉ đọc dữ liệu và cung cấp `WithEpisodeAsync`/`WithAlertAsync`: mở transaction, lấy khóa cùng thứ tự, chạy thay đổi do Application quyết định, lưu và commit. Application phát sự kiện sau khi phương thức ghi đã commit thành công. `SnapshotUploadWorker` gọi `ISafetyMonitoringService`, không tự ghi nghiệp vụ qua repository; mọi Warning mới phải có URL ảnh HTTPS hợp lệ trước khi lưu.

Lịch sử sự cố đi qua `ISafetyIncidentReader`. Application kiểm tra bộ lọc và định nghĩa projection nghiệp vụ dùng chung trong `SafetyIncidentMapper`; Infrastructure dịch projection thành SQL, join người báo, lọc/thống kê/phân trang tại PostgreSQL trong một snapshot đọc. Không tải toàn bộ bảng Incident/User/Profile về Application. URL HTTP phát video được ghép ở API; Application chỉ trả quyền phát/ticket.

## 1. Bắt đầu từ bức tranh tổng thể

Hệ thống không chạy thành một hàm dài nối tiếp. Sau khi backend khởi động, nhiều background worker và các request HTTP chạy đồng thời.

~~~mermaid
flowchart TD
    SOURCE["Camera thật hoặc simulator RTSP"] --> CAMERA["CameraWorker: nhận dữ liệu video"]
    CAMERA --> WEBRTC["CameraWebRtcSource + CameraStreamService"]
    WEBRTC --> VIDEO["Video trong trình duyệt"]
    CAMERA --> DECODE["CameraAnalysisFramePump: giải mã thành JPEG"]
    DECODE --> LIVE["LiveMonitoringWorker: chọn frame mới nhất"]
    LIVE --> PYTHON["AiWorkerClient → Python Detector.infer"]
    PYTHON --> RISK["RiskDecisionEngine.Observe"]
    RISK --> STATE["CameraAnalysisState: trạng thái trong RAM"]
    STATE --> META["CameraAnalysisBroadcastWorker → SignalR"]
    META --> TAGS["Frontend: cập nhật trạng thái AI và nhãn nguy cơ"]
    META --> BOX["Canvas: nội suy box theo RTP frame đang hiển thị"]
    VIDEO --> BOX
    STATE --> PAIRED["CameraAnalysisVideoSource: frame đã vẽ box → H.264"]
    PAIRED --> MONITOR["Video giám sát: hình và box cùng frame"]
    RISK --> OUTBOX["AlertSnapshotQueue + StagingWorker"]
    OUTBOX --> PHOTO["SnapshotUploadWorker: chuẩn bị và upload ảnh"]
    PHOTO --> WARNING["SafetyMonitoringService.RecordAsync → Warning trong DB"]
    WARNING --> POPUP["SignalR → popup và âm báo"]
    POPUP --> ACK["Người dùng xác nhận"]
    ACK --> INCIDENT["Chỉ Khẩn cấp LIVE mới tạo Incident"]
~~~

**Ba nhánh cần nhớ:**

| Nhánh | Đầu ra | Có cần lưu database cho từng frame? |
|---|---|---|
| Phát hình | Video WebRTC | Không |
| Phân tích và hiển thị | Detection, trạng thái nguy cơ, box | Không; dữ liệu frame chủ yếu ở RAM |
| Lưu sự kiện | Warning có ảnh; sau xác nhận có thể có Incident | Có |

Box đã hiện không có nghĩa Warning đã được lưu. Warning đã lưu không có nghĩa Incident đã được tạo. Tắt trang camera không đồng nghĩa backend dừng AI.

## 2. Các khái niệm và đối tượng dữ liệu

| Tên trong code | Hiểu như thế nào? |
|---|---|
| Equipment | Thiết bị trong DB. Camera là Equipment có Category tương ứng |
| CameraWorker | Bộ nhận RTSP của một nguồn camera |
| CameraEncodedFrame | Hình đã nén H.264/H.265/JPEG, kèm RTP timestamp |
| CameraCapturedFrame | JPEG để AI xử lý, có CapturedAt, FrameId và RtpTimestamp |
| TrackedDetection | Kết quả nhận diện: TrackId, Class, Confidence, Box |
| TrackId | Danh tính tạm của người do tracker giữ qua các quan sát |
| RiskDecision | Quyết định WARNING/CRITICAL trên một quan sát hợp lệ |
| Episode | Một đợt nguy cơ của track; không phải mỗi frame là một episode |
| AnalysisSession | Phiên phân tích của nguồn simulator; đổi khi RTSP nối lại |
| CameraAnalysisDto | Metadata đưa kết quả AI từ backend sang frontend |
| PendingAlertSnapshot | Tác vụ ảnh bền vững trên đĩa, chưa phải Warning trong DB |
| Warning / AlertDto | Cảnh báo đã lưu / dữ liệu cảnh báo gửi qua API |
| Incident | Hồ sơ sự cố; là đối tượng khác Warning |

Các loại thời gian không được dùng thay cho nhau:

| Trường | Ý nghĩa |
|---|---|
| CapturedAt / ObservedAt | Thời điểm quan sát phía backend, dùng UTC |
| ProcessedAt | Thời điểm backend cập nhật kết quả phân tích |
| ServerTime | Thời điểm server trả metadata, giúp frontend tính độ cũ |
| RtpTimestamp | Đồng hồ video 90 kHz, số nguyên uint32; không phải ngày giờ |
| FrameId | ID của frame, hiện tạo bằng GUID; không phải số đếm FPS |
| RiskDecision.Timestamp với LIVE | Thời gian quan sát dưới dạng Unix seconds |
| VideoTimestamp với UPLOAD | Vị trí theo giây trong file video |
| Warning.CreatedAt | Thời điểm ghi bản ghi; có thể muộn hơn lúc phát hiện |

Box có dạng **[x1, y1, x2, y2]**, chuẩn hóa theo ảnh nguồn trong khoảng 0–1. Nó không phải dạng [x, y, width, height].

## 3. Bản đồ thư mục

~~~text
backend/src/SmartPool.API/
  Program.cs                         Khởi động host, middleware và route
  Controllers/CamerasController.cs  Danh sách camera, metadata AI và JPEG phân tích
  Controllers/EquipmentsController.cs
                                    CRUD camera và mở/đóng phiên WebRTC
  Controllers/AlertsController.cs   Danh sách, xác nhận, hủy Warning
  Controllers/SafetyAlertsController.cs
                                    Lịch sử và xử lý Incident

backend/src/SmartPool.Application/
  Features/ManageEquipments/        Command/query camera và handler
  Features/SafetyMonitoring/
    RiskDecisionEngine.cs          Quyết định nguy cơ theo thời gian
    MonitoringContracts.cs         AlertDto, command và repository interface
    VideoContracts.cs              Metadata camera/video và handler

backend/src/SmartPool.Infrastructure/
  Cameras/                         Kết nối RTSP, decoder và WebRTC
  SafetyMonitoring/                Điều phối AI, snapshot, video upload, trạng thái RAM
  Persistence/Repositories/MonitoringRepository.cs
                                    Lưu Warning, ACK và tạo Incident
  Services/NotificationHubBase.cs   SignalR và nhóm người nhận

backend/ai/
  worker.py                        ONNX inference, ByteTrack, giao thức với C#
  rendering.py                     Vẽ nhãn và box lên ảnh
  snapshot.py                      Renderer dự phòng cho ảnh snapshot

frontend/thanh-minh-smart-pool/src/
  pages/admin/safety/CameraListPage.jsx
  features/equipments/components/CameraViewModal.jsx
                                    Có export CameraVideoPlayer
  features/safety/                  Hook AI, popup, box, danh sách cảnh báo

tools/camera-simulator/             Phát MP4 thành RTSP và các công cụ kiểm tra
~~~

## 4. Backend khởi động như thế nào?

Đọc [Program.cs](../backend/src/SmartPool.API/Program.cs) trước.

~~~text
Program.Main(args)
  ├─ Tìm và Env.Load(.env)
  ├─ WebApplication.CreateBuilder(args)
  ├─ builder.Configuration.AddEnvironmentVariables()
  ├─ AddApplicationServices(...)
  │    └─ Đăng ký MediatR và các handler ở Application
  ├─ AddInfrastructureServices(...)
  │    └─ Đăng ký DB, camera, worker, trạng thái và repository
  ├─ AddApiAuthorization()
  ├─ builder.Build()
  ├─ CORS → Authentication → Authorization
  ├─ MapControllers()
  ├─ MapHubs() → /hubs/notifications
  ├─ Các bước seed khi khởi động
  └─ app.Run()
       └─ Host quản lý StartAsync / ExecuteAsync của hosted services
~~~

.env được tìm ở thư mục làm việc, sau đó ở AppContext.BaseDirectory. Làm việc ở thư mục khác có thể ảnh hưởng file cấu hình và đường dẫn tương đối.

Đăng ký nằm trong [Infrastructure/DependencyInjection.cs](../backend/src/SmartPool.Infrastructure/DependencyInjection.cs).

| Service | Lifetime / cách chạy | Vai trò |
|---|---|---|
| CameraService | Singleton | Dùng chung registry và RTSP worker |
| CameraSupervisor | Hosted service | Khôi phục camera đang Active từ DB |
| CameraStreamService | Singleton và hosted service | Quản lý peer/view WebRTC và dọn view hết hạn |
| CameraFrameSource | Singleton qua ICameraFrameSource | Dùng chung decoder liên tục của từng camera |
| CameraAnalysisState | Singleton | Nơi trao đổi frame/metadata giữa các nhánh |
| RiskThresholds | Singleton qua IRiskThresholds | Bộ ngưỡng có hiệu lực |
| SafetyThresholdsBootstrap | Hosted service | Nạp và đồng bộ ngưỡng DB |
| LiveMonitoringWorker | Hosted service | Điều phối các AI worker LIVE |
| AlertSnapshotQueue | Singleton | Hàng đợi RAM và outbox trên đĩa |
| AlertSnapshotStagingWorker | Hosted service | Ghi tác vụ RAM xuống đĩa |
| CameraAnalysisBroadcastWorker | Hosted service | Gửi metadata AI |
| SnapshotUploadWorker | Hosted service | Upload ảnh, sau đó lưu Warning |
| VideoAnalysisWorker | Hosted service | Xử lý file video được upload |
| MonitoringRepository | Scoped | Một scope DB riêng cho một công việc/request |

**BackgroundService không cần người dùng gọi API liên tục để hoạt động.** Host khởi động worker; vòng lặp bên trong tiếp tục chạy cho đến khi bị hủy hoặc host dừng.

## 5. Camera đi vào registry bằng hai đường

### 5.1 Khôi phục camera đã có trong DB

Các file: [CameraSupervisor.cs](../backend/src/SmartPool.Infrastructure/Cameras/CameraSupervisor.cs), [CameraService.cs](../backend/src/SmartPool.Infrastructure/Cameras/CameraService.cs).

~~~text
CameraSupervisor.ExecuteAsync()
  ├─ Kiểm tra CameraOptions.AllowedEndpoints
  ├─ Query Equipment chưa xóa, Status=Active, Category=camera
  ├─ CameraService.ReconcileAsync(ids)
  │    └─ Gỡ worker của camera không còn thuộc tập cần chạy
  ├─ CameraMetadata.TryGetProtectedConfiguration(metadata, ...)
  └─ CameraService.RestoreAsync(id, protectedConfiguration, ct)
       ├─ protector.Unprotect(...)
       ├─ Deserialize CameraConnectionConfiguration
       ├─ CameraAddressPolicy.ValidateAsync(...)
       ├─ new CameraWorker(...)
       └─ Ghi vào workers và published
~~~

Supervisor lặp lại khoảng **20 giây**. RestoreAsync bỏ qua camera đã được đăng ký; nó không tạo lại kết nối ở mỗi vòng.

**workers** được quản lý theo identity nguồn; **published** ánh xạ EquipmentId sang worker. Vì vậy request xem camera và AI có thể lấy cùng một nguồn.

Cấu hình kết nối camera được mã hóa bằng Data Protection. Thư mục khóa phải ổn định qua các lần build/chạy. RTSP simulator vẫn phát được ngay cả khi backend không giải mã được cấu hình đã lưu; hai trạng thái này khác nhau.

### 5.2 Đăng ký camera mới

HTTP đi qua EquipmentsController, command/handler ở ManageEquipments, sau đó gọi **CameraService.PrepareAsync()**.

~~~text
PrepareAsync(configuration)
  → ValidateAsync: kiểm tra địa chỉ được phép
  → kiểm tra FFmpeg, trùng nguồn và giới hạn kết nối
  → mã hóa cấu hình
  → new CameraWorker(...)
  → chờ worker.FirstFrame
  → trả ICameraRegistration

Luồng lưu thiết bị sử dụng registration:
  → Publish(equipmentId): gắn worker vào EquipmentId
  → Confirm(): đánh dấu đăng ký hoàn tất
  → DisposeAsync(): nếu chưa Confirm thì dọn worker
~~~

Registration giúp hạn chế tình trạng tạo kết nối camera thành công nhưng lưu thiết bị thất bại rồi để worker chạy thừa.

## 6. CameraWorker nhận RTSP ra sao?

File: [CameraWorker.cs](../backend/src/SmartPool.Infrastructure/Cameras/CameraWorker.cs).

Class này khởi động công việc bằng **Task.Run trong constructor**. Không có hàm CameraWorker.ExecuteAsync như hosted service.

~~~text
new CameraWorker(...)
  → Task.Run(...)
    → vòng lặp kết nối/reconnect
      ├─ new RTSPClient(...)
      ├─ client.NewTrack += callback
      │    └─ CameraKeyFrame.Configure(codec, configuration)
      ├─ client.ReceivedData += callback
      ├─ client.Connect(..., RTPTransport.TCP, credentials)
      ├─ theo dõi StreamingFinished và tuổi dữ liệu nhận được
      └─ nếu mất kết nối: đóng client, đợi retry rồi nối lại
~~~

Trong callback nhận dữ liệu:

~~~text
client.ReceivedData
  → CameraRtpFrameBuffer.Push(rtpTimestamp, receivedAt, parts)
  → CameraKeyFrame.Read(...)
  → CameraRtpClock.Observe(...)
  → nếu cần: gửi keyframe để lấy ảnh preview/kiểm tra sống
  → liveFrames.Invoke(CameraEncodedFrame)
~~~

Các lớp phụ trợ:

| Lớp / hàm | Việc thực hiện |
|---|---|
| CameraRtpFrameBuffer.Push | Gom các phần cùng RTP timestamp thành một picture; trả picture trước khi thấy timestamp kế tiếp |
| CameraKeyFrame.Configure / Read | Giữ thông tin codec, SPS/PPS và nhận dạng dữ liệu picture/keyframe |
| CameraRtpClock.NewSource / Observe | Duy trì đồng hồ 90 kHz qua uint wrap/reconnect |
| CameraFrameDecoder.DecodeAsync | Giải mã một ảnh dùng cho bootstrap/preview |
| CameraWorker.Subscribe | Cho nhánh khác nhận frame nén |
| UpdateDecodedFrame | Dùng ảnh từ decoder liên tục để cập nhật preview và health |

Khi đang có decoder AI liên tục, worker tái sử dụng ảnh của decoder đó thay vì mở thêm FFmpeg định kỳ cho cùng camera.

## 7. Nhánh phát video: frontend mở một phiên WebRTC

Các file:

- [CameraListPage.jsx](../frontend/thanh-minh-smart-pool/src/pages/admin/safety/CameraListPage.jsx).
- [useCameraList.js](../frontend/thanh-minh-smart-pool/src/features/safety/hooks/useCameraList.js).
- [CameraViewModal.jsx](../frontend/thanh-minh-smart-pool/src/features/equipments/components/CameraViewModal.jsx).
- [CameraStreamService.cs](../backend/src/SmartPool.Infrastructure/Cameras/CameraStreamService.cs).

~~~text
CameraListPage
  → useCameraList(filters)
    → monitoringService.cameras(...)
      → GET /api/cameras
        → CamerasController.List
          → sender.Send(GetCameraListQuery)
            → GetCameraListHandler.Handle
              → repository.ListAsync + CameraService.GetStatus
~~~

Danh sách trả cả **ConnectionStatus**, **Source**, **FastReconnect**, **SmoothPlayback**. Đây là thông tin để frontend quyết định cách mở player; không phải dữ liệu video.

Một card hiển thị CameraVideoPlayer. Component này được export từ CameraViewModal.jsx, tên file không phải CameraVideoPlayer.jsx.

~~~text
CameraVideoPlayer: effect kết nối
  → lấy cấu hình ICE
  → tạo RTCPeerConnection và video transceiver
  → createOffer / setLocalDescription / waitForIce
  → equipmentService.openCameraView(id, { type, sdp, analysis, smooth })
    → POST /api/equipments/{id}/camera/views
      → EquipmentsController.OpenCameraView
        → OpenCameraViewHandler.Handle
          → CameraStreamService.OpenAsync
  ← nhận answer SDP và viewId
  → peer.setRemoteDescription(...)
  → peer.ontrack: gắn MediaStream vào video.srcObject
~~~

**CameraStreamService.OpenAsync()** xác thực owner/offer, trạng thái nguồn và cấu hình AI, tạo peer, chọn nguồn video, gắn sự kiện nguồn vào **Viewer.Send()**, rồi trả answer.

### Hai mode AI cần phân biệt

Theo lựa chọn ngày 14/11/2026, thẻ camera, cửa sổ phóng to và chi tiết cảnh báo LIVE ưu tiên `analysis=true, smooth=true` khi nguồn hỗ trợ. Video giữ FPS nguồn; frontend nội suy box giữa các quan sát AI theo RTP của frame đang hiển thị và dùng chung hàm canvas `drawVideoRisks` với video upload. Camera không dừng phát để chờ mỗi lần suy luận AI. Khi nguồn/trình duyệt không hỗ trợ đồng bộ RTP, player chuyển sang frame đã vẽ box qua `CameraAnalysisVideoSource`; FPS nhánh dự phòng phụ thuộc AI.

Nếu trình duyệt hỗ trợ `VideoFrame`, player giữ một hàng đợi hình ngắn qua `CameraPlayoutBuffer`. `requestVideoFrameCallback` thu frame cùng RTP; `requestAnimationFrame` lấy frame đến hạn, lấy box theo chính RTP đó, rồi `drawVideoRisks(..., frame)` vẽ cả hình và box cùng canvas. Độ trễ mục tiêu dựa trên thời gian phân tích và khoảng cách giữa các quan sát, giới hạn 160–550 ms (ban đầu 300 ms); thay đổi dần khi phát để tránh đứng/nhảy hình do đổi độ trễ. Đây là thời gian giữ thêm ở frontend, không phải số đo trễ toàn hệ thống. Jitter buffer WebRTC được gợi ý 60 ms thay cho 400 ms khi dùng hàng đợi này. cameraCapturePlan tính số frame cần giữ theo FPS và thời gian đệm (4–40 frame), rồi co hình về kích thước hiển thị trong ngân sách RGBA ước tính 48 MiB/viewer. Không cắt xuống 6 frame chỉ vì nguồn là 720p, vì sẽ mất khoảng nội suy. Ước lượng FPS dùng khoảng RTP ngắn nhất của 12 mẫu gần nhất để một frame bị rơi không làm giảm dung lượng đệm. Đầy hàng đợi thì giảm thời gian chờ để vẫn phát và giới hạn tài nguyên. Frame bị bỏ, frame đã thay thế và mọi frame còn giữ khi đóng viewer đều được `close()`. Trình duyệt thiếu `VideoFrame` dùng canvas phủ trực tiếp theo RTP như trước.

| Điều kiện | Nguồn | Box xuất hiện bằng cách nào? |
|---|---|---|
| analysis=true, smooth=true và nguồn có SmoothPlayback | CameraWebRtcSource | Video gốc; frontend ghép canvas theo RTP |
| analysis=true nhưng không dùng được smooth | CameraAnalysisVideoSource | Pixel JPEG đã có box được encode lại thành H.264 |
| analysis=false | CameraWebRtcSource | Preview nguồn; không yêu cầu phân tích AI cho phiên xem |

CameraWebRtcSource dùng passthrough với H.264 tương thích; nguồn khác có thể cần FFmpeg chuyển mã. CameraAnalysisVideoSource nhận JPEG từ CameraAnalysisState.FrameReady rồi encode. Nhiều viewer của cùng camera/mode dùng chung source.

~~~text
source.Frame event
  → Viewer.Send(bytes, key, duration, captureTimestamp)
    → chờ keyframe nếu peer chưa sẵn sàng
    → SendCapturedFrame(...) nếu có captureTimestamp
       └─ packetize H.264, Peer.SendRtpRaw với timestamp gốc đã chuẩn hóa
    → Peer.SendVideo(...) với nguồn không mang captureTimestamp
~~~

CameraStreamService.ExecuteAsync chủ yếu dọn các view hết hạn/lỗi. Chính callback source.Frame mới chuyển frame sang peer.

Player theo dõi phiên qua API views/{viewId}, đóng phiên bằng DELETE và tự retry có giới hạn. Trong gián đoạn simulator ngắn, backend có cơ chế giữ view để tránh thương lượng WebRTC lại ngay.

## 8. Nhánh AI: lấy JPEG liên tục, chọn frame mới nhất

Các file: [LiveMonitoringWorker.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/LiveMonitoringWorker.cs), [CameraFrameSource.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/CameraFrameSource.cs), [CameraAnalysisFramePump.cs](../backend/src/SmartPool.Infrastructure/Cameras/CameraAnalysisFramePump.cs).

~~~text
LiveMonitoringWorker.ExecuteAsync()
  → mỗi khoảng 1 giây: tìm camera Active cần giám sát
  → LiveCameraSelection ưu tiên camera Connected, ổn định các worker đang chạy
  → chỉ giữ slot camera mất kết nối ngắn trong LiveReconnectGraceSeconds (mặc định 15 giây)
  → giới hạn theo MaxLiveCameras
  → RunCameraAsync(cameraId, token) cho từng camera
  → hủy task và gỡ metadata nếu camera không còn thuộc tập cần chạy
~~~

**RunCameraAsync()** là vòng đời một camera. Các camera có Python worker/tracker riêng. C# dùng tác vụ bất đồng bộ và process Python; không phải tất cả camera cùng chờ một vòng inference tuần tự.

Đường lấy ảnh:

~~~text
RunCameraAsync
  → frames.Capture(cameraId)
    → CameraFrameSource.Capture
      → CaptureCore
        ├─ dọn pump đã lỗi, camera offline hoặc đổi session
        ├─ tạo CameraAnalysisFramePump nếu chưa có
        └─ trả pump.Latest nếu còn đủ mới

new CameraAnalysisFramePump(...)
  → CameraService.Subscribe(id, Offer, continuousDecoder: true)
  → Task.Run(RunAsync)

Offer(encodedFrame)
  → đưa frame nén và thời điểm vào channel có giới hạn

RunAsync
  → chờ keyframe để mở FFmpeg
  → ghi frame nén vào stdin FFmpeg
  → ReadJpegsAsync đọc JPEG từ stdout
  → lấy timestamp tương ứng từ pending queue
  → cập nhật Latest
  → CameraService.UpdateDecodedFrame
~~~

**Latest** chỉ là frame mới nhất. AI xử lý xong một lần sẽ lấy frame mới nhất tiếp theo, không cố phân tích hết mọi frame cũ đang trôi qua.

Channel đầu vào vẫn phải giữ chuỗi frame nén cần thiết để decoder giải mã P-frame. Bỏ qua một JPEG ở bước inference khác với tự ý bỏ một P-frame trước decoder. Khi backlog vượt giới hạn, pump khởi động lại để đồng bộ lại.

**Điểm cần lưu ý:** JPEG được gán timestamp theo thứ tự pending queue. Khi FFmpeg báo lỗi giải mã, pump bị hủy, Latest được xóa và decoder tạo lại từ keyframe; không tiếp tục FIFO sau khi decoder đã bỏ picture lỗi. Test CameraPixelAlignmentTests đã đối chiếu ID mã hóa trong pixel với RTP trên H.264 baseline cả bình thường và cố tình hỏng picture. Đây chưa phải bảo đảm cho mọi codec/B-frame/reorder; chưa truyền PTS xuyên container.

## 9. C# gọi Python và model như thế nào?

Các file: [AiWorkerClient.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/AiWorkerClient.cs), [worker.py](../backend/ai/worker.py).

~~~text
RunCameraAsync
  → new AiWorkerClient(options, inferenceThreads: ...)
    → Process.Start(python -u worker.py model.onnx font)
  → ai.ReadyAsync(token)
  → ai.InferAsync(cameraId, captured.Jpeg, resetTracker, token)
  → AiWorkerClient.Detections(result)
  → engine.Observe(sourceTimestamp, detections)
  → ai.RenderAsync(decisions, token) nếu có box cần render
~~~

AiWorkerClient dùng **stdin/stdout của process**, không gọi một HTTP AI server.

Một lệnh LIVE có dạng minh họa:

~~~text
JSON header: {"op":"infer","key":"<camera-id>","resetTracker":false,"jpegLength":12345}
newline
12345 byte JPEG
~~~

Python đọc đúng số byte, trả JSON detections. Lệnh render có thể trả JSON header chứa jpegLength rồi đến byte JPEG. PipeReader của C# đọc cả JSON lẫn binary theo giao thức này. Stderr được đọc riêng, không được lẫn vào stdout giao thức.

Trong Python:

~~~text
main(protocol)
  → Detector(model_path)
    → ort.InferenceSession(...)
    → đọc tên class và kích thước input từ model
  → chạy một lượt warm-up
  → trả {"ready": true, ...}
  → vòng lặp đọc request
    → op="infer"
      → đọc JPEG, cv2.imdecode
      → reset tracker theo key nếu được yêu cầu
      → Detector.infer(image, key)
~~~

**Detector.infer()** làm lần lượt:

1. Resize có giữ tỷ lệ và letterbox về input model.
2. Đổi BGR sang RGB, CHW, float32 và chuẩn hóa 0–1.
3. Gọi ONNX Runtime session.run.
4. Lấy class và score, lọc detection ban đầu.
5. Chạy NMS để giảm các box chồng trùng.
6. Đổi box từ ảnh model về ảnh nguồn.
7. CameraTracker.update nối danh tính qua các quan sát.
8. Trả TrackId, Class, Confidence và Box chuẩn hóa.

Class **CameraTracker** kế thừa ByteTrack và có phần xử lý box lồng nhau/ghép lại quan sát. Khi có observation_box của frame hiện tại, output ưu tiên box quan sát đó.

Model được kiểm tra phải có hai class **normal** và **drowning**. Detector dùng ngưỡng đầu vào thấp để tracker còn có thể nối track; ngưỡng đó khác ConfidenceThreshold của RiskDecisionEngine.

Provider inference được chọn từ cấu hình và các provider ONNX Runtime thực sự có sẵn. DirectML được ưu tiên nếu môi trường hỗ trợ; có đường CPU fallback. Không thể kết luận đang dùng GPU chỉ vì máy có GPU.

## 10. RiskDecisionEngine quyết định mức cảnh báo

File trọng tâm: [RiskDecisionEngine.cs](../backend/src/SmartPool.Application/Features/SafetyMonitoring/RiskDecisionEngine.cs).

Đây là logic C#, tách khỏi model ONNX. Model đưa nhận diện từng quan sát; engine quyết định nguy cơ dựa trên lịch sử.

~~~text
Observe(timestamp, detections)
  → loại quan sát không hợp lệ / dưới ConfidenceThreshold
  → tìm hoặc tạo State theo TrackId
  → kiểm tra timestamp và khoảng mất quan sát
  → thêm (Time, Drowning) vào Window
  → loại quan sát quá WindowSeconds
  → chưa đủ MinObservations: chưa đưa ra quyết định mới
  → tính ratio
  → xét WARNING, CRITICAL, recovery, cooldown
  → trả RiskDecision cho track đang có severity khác NORMAL
~~~

Mặc định trong RiskOptions:

| Tham số | Giá trị | Ý nghĩa |
|---|---:|---|
| WindowSeconds | 5 giây | Cửa sổ quan sát |
| ConfidenceThreshold | 0,60 | Confidence tối thiểu để engine nhận quan sát |
| MinObservations | 3 | Số quan sát hợp lệ tối thiểu |
| WarningRatio | 0,40 | Ngưỡng lên Chú ý |
| CriticalRatio | 0,70 | Ngưỡng bắt đầu/duy trì bộ đếm lên Khẩn cấp |
| CriticalHoldSeconds | 3 giây | Thời gian giữ điều kiện lên Khẩn cấp |
| RecoveryRatio | 0,20 | Ngưỡng có thể phục hồi |
| RecoveryHoldSeconds | 5 giây | Thời gian cần duy trì điều kiện phục hồi |
| MaxObservationGapSeconds | 3 giây | Khoảng gián đoạn làm reset cửa sổ và các bộ đếm hold |
| CooldownSeconds | 15 giây | Hạn chế kích hoạt lại theo các nhánh nghiệp vụ |

~~~text
ratio = số quan sát drowning hợp lệ trong Window
        / tổng số quan sát hợp lệ trong Window
~~~

Ratio không phải confidence trung bình, cũng không phải phần trăm thời gian của video.

Ví dụ minh họa với ba quan sát đầu ở giây 0, 1, 2 đều đủ confidence và đều drowning:

| Thời điểm | Kết quả điển hình theo mặc định |
|---|---|
| 0, 1 | Chưa đủ 3 quan sát |
| 2 | WARNING; bắt đầu CriticalSince nếu ratio đủ cao |
| 3, 4 | Tiếp tục WARNING nếu chưa giữ ngưỡng đủ 3 giây |
| 5 | CRITICAL nếu điều kiện được giữ đủ |

Ví dụ dùng nhịp 1 quan sát/giây để dễ tính; đây không phải FPS AI của ứng dụng.

Các điểm dễ hiểu sai:

- Cửa sổ 5 giây không bắt buộc luôn chờ đủ 5 giây mới có WARNING.
- CRITICAL được giữ qua vùng tỷ lệ WARNING; engine không có nhánh tự hạ CRITICAL về WARNING chỉ vì ratio còn 50%.
- Về NORMAL cần ratio đủ thấp và giữ đủ thời gian phục hồi.
- Confidence thấp hoặc không thấy track không được tính là một quan sát normal.
- Khoảng gián đoạn lớn có thể reset window/hold nhưng giữ severity và episode; không xác nhận rằng người đã an toàn.
- ActiveTrackIds() chỉ đọc những track có trạng thái nguy cơ đang giữ. Nó không tạo một RiskDecision mới để lưu DB.

Ngưỡng có thể khác bảng trên vì [RiskThresholds.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/RiskThresholds.cs) nạp cấu hình DB. SafetyThresholdsBootstrap.StartAsync nạp lúc khởi động và ExecuteAsync kiểm tra khoảng 5 giây/lần. SaveAsync áp ngưỡng bằng CopyFrom dưới lock; mỗi Observe lấy một Clone nhất quán của toàn bộ bộ ngưỡng. Các engine không bị tạo mới chỉ vì đổi ngưỡng.

## 11. Kết quả AI được đưa ra giao diện bằng đường nào?

File: [CameraAnalysisState.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/CameraAnalysisState.cs).

Sau khi phân tích:

~~~text
LiveMonitoringWorker.RunCameraAsync
  → state.Set(cameraId, frameId, capturedAt, decisions, annotatedJpeg,
              detections, inferenceMs, rtpTimestamp, trackingSession, activeTrackIds)
    ├─ cập nhật metadata và JPEG mới nhất trong RAM
    ├─ giữ một số frame gần nhất để GET JPEG theo FrameId
    └─ phát FrameReady
~~~

Hiện state giữ tối đa 8 frame gần nhất cho mỗi camera và xét tuổi frame ở đường đọc. Không nên lưu một FrameId cũ rồi chờ rất lâu mới tải ảnh.

FrameReady được nguồn paired video sử dụng. Metadata có đường gửi khác:

~~~text
CameraAnalysisBroadcastWorker.ExecuteAsync
  → state.Active()
  → bỏ frameId đã gửi
  → INotificationService.SendToGroupAsync(
        "camera-analysis_<camera-guid-không-dấu-gạch>",
        "camera_analysis", metadata)
~~~

Broadcaster đợi khoảng 75 ms giữa các vòng có dữ liệu, gộp về frame mới nhất. Gửi song song tối đa 8 camera, mỗi lần gửi có scope và timeout 200 ms riêng; camera gửi chậm không giữ các camera còn lại theo thứ tự. Nó không gửi mọi kết quả inference một cách bắt buộc.

Frontend:

~~~text
useCameraRisks(cameraId, enabled)
  ├─ useQuery → monitoringService.analysis → GET /api/cameras/{id}/analysis
  │    └─ fallback/đồng bộ khoảng 1 giây
  └─ subscribeCameraAnalysis(cameraId, callback)
       → NotificationHubBase.SubscribeCameraAnalysis(cameraId)
       → nhận "camera_analysis"
       → mergeCameraAnalysis(...)
       → CameraRiskFrameBuffer.add(...)
~~~

[NotificationHubBase.cs](../backend/src/SmartPool.Infrastructure/Services/NotificationHubBase.cs) chỉ cho ADMIN đăng ký nhóm metadata camera. Nhóm metadata này khác nhóm nhận Warning/popup.

Metadata quan trọng gồm State, FrameId, ObservedAt, ProcessedAt, ServerTime, RtpTimestamp, TrackingSession, Detections, Risks và ActiveRiskTrackIds.

Field Synchronization trong DTO không nên được dùng một mình để suy ra mode video. Mode được quyết định bởi offer analysis/smooth và khả năng SmoothPlayback của nguồn.

## 12. Frontend vẽ box ra sao?

**Luồng ưu tiên hiện tại:** camera dùng canvas nội suy theo RTP để giữ FPS nguồn. Cả camera và upload đang phân tích gọi `drawVideoRisks`; camera lấy mẫu timeline RTP, upload lấy mẫu theo chỉ số frame/thời gian video và có thể chờ metadata. Video upload hoàn chỉnh và camera ở nhánh dự phòng đã có box trong pixel.

Với bộ đệm `VideoFrame`, canvas còn vẽ chính hình đang giữ cùng box; không lấy tọa độ của frame đã đệm để phủ lên hình live mới hơn. Kích thước/letterbox được tính theo frame đang vẽ, kể cả khi nguồn đã thay đổi độ phân giải. Mất frame video trên 500 ms sẽ xóa hàng đợi và box; không phát lại các hình tồn đọng để giả như camera vẫn đang chạy.

Đọc lần lượt:

1. [useCameraRisks.js](../frontend/thanh-minh-smart-pool/src/features/safety/hooks/useCameraRisks.js).
2. [cameraRiskFrameBuffer.js](../frontend/thanh-minh-smart-pool/src/features/safety/utils/cameraRiskFrameBuffer.js).
3. [cameraBoxInterpolation.js](../frontend/thanh-minh-smart-pool/src/features/safety/utils/cameraBoxInterpolation.js).
4. Hàm dùng chung [drawVideoRisks.js](../frontend/thanh-minh-smart-pool/src/features/safety/utils/drawVideoRisks.js), được gọi từ CameraViewModal và luồng upload đang phân tích.

~~~text
video.requestVideoFrameCallback(draw)
  → metadata.rtpTimestamp của frame đang được trình bày
  → sampleRisks(timestamp)
    → CameraRiskFrameBuffer.sample(timestamp)
      → tìm quan sát trước / sau timestamp
      → chọn risk từ quan sát quá khứ
      → nội suy tọa độ cùng TrackId
      → trả danh sách box
  → paintOverlay()
    → drawVideoRisks(canvas, video, risks)
      → đổi tọa độ chuẩn hóa sang pixel
      → bù vùng nền do object-fit: contain
      → strokeRect + nhãn, cam hoặc đỏ
~~~

Buffer giữ tối đa 32 mẫu. Đổi TrackingSession hoặc nhận trạng thái không Connected sẽ xóa buffer.

Quy tắc hiển thị hiện có:

| Trường hợp | Hành vi |
|---|---|
| Có mẫu trước/sau cùng track | Nội suy theo RTP |
| Có đủ quan sát lân cận | Cubic đơn điệu, đi qua đúng endpoint đã đo |
| Chỉ có hai điểm | Nội suy tuyến tính |
| Chưa có điểm tương lai | Dự đoán chuyển động ngắn, tối đa 200 ms, rồi giữ |
| Mẫu quá xa video | Ẩn sau giới hạn hold 600 ms |
| Không có decision nhưng track vẫn active và có detection mới | Giữ mức nguy cơ đã chốt, cập nhật geometry theo detection thật, kể cả confidence chưa đủ để tạo quyết định mới |
| Không còn detection thật | Chỉ giữ geometry tối đa 300 ms từ lần thấy người cuối; không tự gia hạn bằng box giữ lại |
| Metadata quá cũ | MAX_OVERLAY_AGE_MS là 1000 ms |
| Không có video frame mới quá khoảng 500 ms | Timer của player xóa canvas |

Cache mức nguy cơ và geometry tách khỏi buffer 32 mẫu, vì vậy confidence thấp kéo dài không làm box mất nếu vẫn có detection mới và backend vẫn báo active. Cache được xóa theo recovery, track/session thay đổi hoặc offline; không dùng cache hiển thị làm quan sát mới để lưu cảnh báo.

Backend dùng LiveRiskOverlay riêng cho mỗi camera/session để giữ mức đã chốt trên geometry thực. Metadata có DisplayRisks cho geometry của frame hiện tại, giúp cửa sổ mới mở dựng box ngay cả khi Risks đang rỗng vì confidence thấp. Risks vẫn là bằng chứng thật từ RiskDecisionEngine; chỉ danh sách này được đưa vào RecordRiskCommand. Ảnh paired cũng render cùng trạng thái hiển thị để không mất box riêng ở chế độ phát này.

Mỗi useCameraRisks có timeline riêng nhưng dùng chung query cache. Effect theo ai.data nạp metadata vào timeline của từng viewer, kể cả request HTTP được observer ở card camera thực hiện. Nhờ vậy popup mới mở không phải chờ thêm một event hoặc một quyết định nguy cơ mới để vẽ box.

Position/size không còn đi qua CameraBoxSmoother kiểu áp dần 80/110 ms. Hàm này đã được loại bỏ khỏi đường vẽ hiện tại.

**Nội suy không phải một lần chạy AI mới.** Vẽ 25 lần/giây không có nghĩa model đã nhận diện 25 lần/giây. Khớp tọa độ AI cũng không tự chứng minh model đã nhận đúng người hoặc timestamp đã gắn đúng nội dung ảnh.

## 13. Nhánh lưu ảnh và Warning

Các file:

- [AlertSnapshotQueue.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/AlertSnapshotQueue.cs).
- [AlertSnapshotStagingWorker.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/AlertSnapshotStagingWorker.cs).
- [SnapshotUploadWorker.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/SnapshotUploadWorker.cs).
- [MonitoringRepository.cs](../backend/src/SmartPool.Infrastructure/Persistence/Repositories/MonitoringRepository.cs).

~~~text
foreach decision do engine.Observe trả về
  → snapshots.TryStage(RecordRiskCommand, captured.Jpeg, annotatedJpeg)
    → channel RAM có giới hạn, không ghi đĩa trong hot path
  → AlertSnapshotStagingWorker.ExecuteAsync
    → queue.StageAsync(...)
      → ghi source.jpg, boxes.json, JPEG đã render nếu có
      → ghi pending-alert.json
      → đánh thức consumer upload

SnapshotUploadWorker.ExecuteAsync
  ├─ RunOutboxAsync × 3
  │    → ProcessOneOutboxAsync
  │      → lấy lease file và khóa episode trong process
  │      → repository.PrepareSnapshotAsync
  │      → nếu không cần ảnh mới: dọn job
  │      → nếu cần: dùng JPEG render sẵn, hoặc RenderAsync dự phòng
  │      → Cloudinary.UploadAsync
  │      → lưu ImageUrl vào job để phục hồi/retry
  │      → repository.RecordAsync
  │      → dọn outbox item
  └─ RunLegacyAsync: phục hồi các Warning cũ chưa hoàn tất ảnh
~~~

LIVE đã render ảnh trong worker AI nên có thể tái sử dụng JPEG, tránh mở Python vẽ lại cho từng snapshot. Ảnh có thể chứa các box nguy cơ của cùng frame; Warning vẫn có Box/TrackId riêng.

RAM có 2 hàng đợi, mỗi hàng tối đa 16 item: riêng LIVE CRITICAL và phần còn lại. Outbox thông thường mặc định 256 item và dự phòng thêm tối đa 16 item dành cho LIVE CRITICAL. StagingWorker giữ và retry mục đã nhận khi đĩa/outbox chưa sẵn sàng. Khi RAM đầy, TryStage trả false, tăng RejectedSnapshots và ghi log có giới hạn; đây không phải cam kết đã lưu bền vững. Mỗi tổ hợp nguồn/track/episode/severity LIVE được chống lặp theo slot giây trước khi vào outbox.

Hosted services được đăng ký để khi tắt host: dừng producer LIVE/video trước, drain staging trong thời gian hữu hạn rồi mới dừng uploader. RAM vẫn có thể mất khi tiến trình bị kill/crash hoặc hết ngân sách shutdown trong lúc đĩa không ghi được; chỉ file outbox đã ghi mới là tác vụ bền vững.

Ba consumer giúp một upload chậm không tuần tự chặn mọi camera. Tác vụ cùng nguồn/track/episode không được xử lý upload đồng thời trong một process. Ưu tiên LIVE CRITICAL, LIVE WARNING rồi đến các tác vụ UPLOAD.

**PrepareSnapshotAsync chỉ quyết định có cần xử lý ảnh mới và target ID; chưa tạo Warning thiếu ảnh.**

**RecordAsync chỉ cho ghi khi ImageUrl là URL HTTPS hợp lệ.** Ảnh/upload lỗi sẽ giữ job để retry/backoff, thay vì chèn Warning mới không có ảnh. Outbox có backoff tăng dần đến 60 giây; số Attempts bị giới hạn giá trị bộ đếm, không phải cứ tới 30 là tự xóa tác vụ. Luồng phục hồi legacy có quy tắc thử ảnh riêng.

Asset mới dùng ID của outbox item để một ảnh CRITICAL đến sau không ghi đè asset của WARNING đã xử lý trong cuộc đua upload/ACK.

## 14. RecordAsync chống trùng và nâng mức như thế nào?

Trong SafetyMonitoringService.RecordAsync:

~~~text
kiểm tra SourceType, severity và ImageUrl
  → tạo root hash từ SourceType + SourceId + TrackId + Episode
  → mở transaction
  → lấy PostgreSQL advisory lock theo root
  → tìm Warning mới nhất cùng root
  → quyết định tạo mới / nâng mức / bỏ qua
  → SaveChangesAsync
  → CommitAsync
  → đọc AlertDto
  → PublishAsync nếu là LIVE và có tạo mới/nâng mức
~~~

| Tình trạng | Xử lý |
|---|---|
| Chưa có Warning cùng episode | Tạo PENDING với ảnh |
| Đã có PENDING cùng mức | Không tạo thêm mỗi frame |
| PENDING WARNING, nhận CRITICAL | Nâng cùng Warning ID, thay ảnh/thông tin tương ứng |
| WARNING đã ACK, sau đó có CRITICAL | Có thể tạo Warning CRITICAL mới, PENDING |
| CRITICAL đã ACK, cùng episode tiếp tục CRITICAL | Không tự tạo Incident/cảnh báo trùng |
| LIVE bị CANCEL, hết cooldown và vẫn có nguy cơ | Có thể tạo một bản ghi cảnh báo mới |
| UPLOAD đã xử lý, cùng mức lặp lại | Không dùng cơ chế tự re-arm LIVE |

RiskEpisode trong DB chứa root hash và một phần định danh bản ghi. Nó không nhất thiết bằng chuỗi Episode thô mà engine gửi.

WarningTime/ObservedAt là lúc quan sát; CreatedAt là lúc lưu. Khi có backlog hoặc upload chậm, hai mốc có thể khác nhau đáng kể.

## 15. Warning tới popup bằng đường nào?

~~~text
SafetyMonitoringService.PublishAsync
  → INotificationService.SendToGroupAsync("safety-operators", event, AlertDto)
    → /hubs/notifications
      → useGlobalSafetyAlerts
        → cập nhật danh sách popup
          → GlobalSafetyAlertsHost
            → GlobalAlertPopup cho từng Alert ID
~~~

Các file: [useGlobalSafetyAlerts.js](../frontend/thanh-minh-smart-pool/src/features/safety/hooks/useGlobalSafetyAlerts.js), [GlobalSafetyAlertsHost.jsx](../frontend/thanh-minh-smart-pool/src/features/safety/components/GlobalSafetyAlertsHost.jsx), [GlobalAlertPopup.jsx](../frontend/thanh-minh-smart-pool/src/features/safety/components/GlobalAlertPopup.jsx).

Host được mount trong cả MainLayout và AdminLayout. Nó chỉ bật với vai trò ADMIN/LIFEGUARD. Backend cũng chỉ thêm các vai này vào nhóm safety-operators.

| Event | Khi phát |
|---|---|
| alert_created | Tạo Warning LIVE mới |
| alert_escalated | Nâng Warning LIVE lên CRITICAL |
| alert_updated | Warning LIVE được cập nhật/xử lý |
| incident_created | ACK LIVE Khẩn cấp đã tạo Incident |

Quy tắc frontend:

- Chỉ nhận popup toàn cục nguồn LIVE.
- Cùng Alert ID cập nhật một popup; không sinh thêm popup chỉ vì nhận event lặp.
- Sắp theo CreatedAt mới trước; có nhiều popup đồng thời.
- Mỗi phiên mức cảnh báo hết hạn sau 10 giây; cập nhật cùng mức không kéo dài timer, nâng WARNING → CRITICAL tạo timer mới.
- Dismiss theo Alert ID + severity: đóng Chú ý không chặn lần nâng Khẩn cấp, nhưng polling không mở lại cùng phiên mức đã đóng.
- SignalR stop/start được tuần tự hóa; logout đóng transport, đổi tài khoản/token xác thực lại rồi tham gia lại nhóm đang subscribe.
- Polling PENDING mỗi 15 giây và resync khi SignalR nối lại.
- Popup mới kiểm tra tuổi ObservedAt, fallback CreatedAt với dữ liệu cũ; cửa sổ nhận hiện là hai phút.
- Đóng hoặc hết hạn popup không đổi trạng thái Warning trong DB.
- Nếu SignalR lỗi, polling là đường khôi phục; không còn bảo đảm tốc độ như push.

**Còi** chạy theo các popup đang hiện, ưu tiên Khẩn cấp nếu có. **Giọng đọc TTS** nhận incident_created hoặc sự kiện local sau ACK, chỉ đọc Incident LIVE Khẩn cấp đã xác nhận và chống đọc trùng theo IncidentId.

## 16. Xem chi tiết, xác nhận, hủy và tạo Incident

### 16.1 Xem chi tiết

| Nơi bấm | Đường chạy |
|---|---|
| Popup LIVE, ADMIN | GlobalAlertPopup.detail → GlobalSafetyAlertsHost.viewLive → LiveAlertDetailsModal → CameraVideoPlayer phát camera ngay trong cửa sổ, không chuyển route |
| Trang Cảnh báo camera | AlertList → monitoringService.alert → GET /api/alerts/{id} → modal ảnh lúc phát hiện |
| Popup video trong màn hình phân tích | Điều hướng tới job và vị trí video tương ứng |

Video LIVE khi xem chi tiết là hình hiện tại. Ảnh trong lịch sử là thời điểm phát hiện; hai nội dung không bắt buộc giống nhau.

[LiveAlertDetailsModal.jsx](../frontend/thanh-minh-smart-pool/src/features/safety/components/LiveAlertDetailsModal.jsx) tải đúng camera theo cameraId, dùng lại useCameraRisks/CameraVideoPlayer để phát video và box. Dialog hiển thị mức cảnh báo, trạng thái, khu vực, đối tượng và thời điểm phát hiện; cập nhật Alert mỗi 2 giây và qua các event invalidation. Ảnh tại thời điểm phát hiện có mục mở riêng để không nhầm với video hiện tại.

Mở chi tiết chỉ dismiss thông báo local, không ACK/CANCEL. Dialog được lưu riêng ở host nên không bị đóng khi notification hết 10 giây. Đóng hoặc xử lý xong sẽ unmount player và DELETE phiên WebRTC. Đổi phiên đăng nhập không giữ lại dialog của phiên cũ.

Trên giao diện, canHandleAlert chỉ cho thao tác với LIVE còn PENDING và tuổi ObservedAt (fallback CreatedAt) nhỏ hơn 10 giây. ObservedAt được cập nhật khi nâng mức, nên mức mới có cửa sổ thao tác mới. useAlertActionClock tự cập nhật nút trong danh sách và dialog mà không chờ refetch HTTP. Từ mốc 10 giây, dòng cảnh báo chỉ còn Xem chi tiết; dialog tiếp tục xem nhưng bỏ Xác nhận/Hủy. Handler kiểm tra lại tuổi tại lúc bấm, kể cả Popconfirm được mở trước khi hết hạn. Video UPLOAD dùng quy tắc duyệt offline riêng.

### 16.2 Xác nhận

Các file chính: [AlertsController.cs](../backend/src/SmartPool.API/Controllers/AlertsController.cs), [MonitoringContracts.cs](../backend/src/SmartPool.Application/Features/SafetyMonitoring/MonitoringContracts.cs), MonitoringRepository.cs.

~~~text
AlertActionButtons.handle (popup/dialog LIVE) / AlertList action
  → monitoringService.handleAlert(id, "acknowledge", severity đang hiển thị)
  → PATCH /api/alerts/{id}/acknowledge
       body: {"expectedSeverity":"WARNING"}
  → AlertsController.Acknowledge
  → sender.Send(HandleAlertCommand)
  → HandleAlertHandler.Handle
  → SafetyMonitoringService.HandleAsync(...)
~~~

Trong HandleAsync:

1. Mở transaction.
2. Lấy advisory lock cùng episode như RecordAsync.
3. Lấy Warning bằng SELECT ... FOR UPDATE, reload giá trị hiện tại.
4. Kiểm tra quyền, PENDING và expectedSeverity.
5. Kiểm tra ảnh khi ACK.
6. Ghi Status, VerifiedBy, VerifiedAt.
7. Nếu ACK + LIVE + CRITICAL/EMERGENCY và chưa có Incident: tạo Incident.
8. SaveChanges, commit; trả DTO và phát event.

| Thao tác | Warning | Incident |
|---|---|---|
| ACK WARNING | ACKNOWLEDGED | Không tạo |
| ACK LIVE CRITICAL/EMERGENCY | ACKNOWLEDGED | Tạo đúng một hồ sơ |
| ACK UPLOAD | ACKNOWLEDGED | Không tạo |
| Cancel | CANCELLED | Không tạo |
| Đóng popup / timeout | Không đổi | Không tạo |

Incident mới có CameraId, WarningId, Location, thời điểm và ảnh liên quan; SeverityLevel=EMERGENCY, Status=Pending. Pending ở đây là trạng thái hồ sơ sự cố, không phải Warning còn chờ xác nhận.

**ACK Warning không làm người tự an toàn và không tự xóa box.** RiskEngine vẫn xét các quan sát tiếp theo. Trạng thái xử lý của người dùng và trạng thái nguy cơ AI là hai việc khác nhau.

### 16.3 Tránh “popup Chú ý nhưng xác nhận lại tạo Incident”

~~~mermaid
sequenceDiagram
    participant U as Người vận hành
    participant UI as Popup
    participant API as AlertsController/Repository
    participant DB as Database
    UI->>U: Hiển thị WARNING
    Note over API,DB: AI đã nâng cùng Warning ID lên CRITICAL
    U->>UI: Bấm Xác nhận
    UI->>API: ACK expectedSeverity=WARNING
    API->>DB: Khóa và đọc mức hiện tại
    DB-->>API: CRITICAL
    API-->>UI: 409 + Alert hiện tại, chưa ACK/chưa Incident
    UI->>U: Cập nhật Khẩn cấp, yêu cầu xem lại
    U->>UI: Xác nhận lần nữa
    UI->>API: ACK expectedSeverity=CRITICAL
    API->>DB: ACK và tạo Incident trong transaction
    API-->>UI: Thành công + incidentId
~~~

Thiếu expectedSeverity ở HTTP acknowledge hoặc cancel hiện bị từ chối 400. Mức khác hiện tại trả 409 kèm Alert. monitoringService phát safety:alert-refreshed để popup cập nhật ngay; không tự xử lý lần hai.

Nếu ACK WARNING hoàn tất trước khi ảnh CRITICAL upload xong, cảnh báo CRITICAL đến sau có ID mới còn PENDING. ID của WARNING đã xử lý không được tái sử dụng cho bản ghi mới.

### 16.4 Xử lý hồ sơ Incident sau đó

Lịch sử đi qua SafetyAlertsController và các handler ở ManageSafetyAlerts, khác đường /api/alerts.

GET /api/safety-alerts/incidents và GET /api/safety-alerts/incidents/{id} đọc hồ sơ. PUT /api/safety-alerts/incidents/{id}/confirm gọi ConfirmSafetyIncidentHandler để cập nhật hồ sơ theo request.

Xác nhận Warning để tạo Incident và xác nhận xử lý một Incident là hai bước nghiệp vụ khác nhau.

## 17. Phân tích video upload là một luồng riêng

Các file: [VideoAnalysisService.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/VideoAnalysisService.cs), [VideoAnalysisWorker.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/VideoAnalysisWorker.cs), [VideoAnalysisState.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/VideoAnalysisState.cs).

~~~text
POST /api/video-analyses
  → VideoAnalysesController.Upload
  → UploadVideoHandler.Handle
  → VideoAnalysisService.UploadAsync
    → kiểm tra cấu hình/đuôi file/dung lượng/giới hạn job
    → tạo job UPLOADING và ghi file
    → VideoProbe.InspectAsync
    → đổi job thành QUEUED
    → VideoAnalysisQueue.Notify

VideoAnalysisWorker.ExecuteAsync → RunAsync
  → claim một job QUEUED bằng FOR UPDATE SKIP LOCKED
  → đổi PROCESSING
  → kiểm tra video/timeline
  → khởi động hoặc dùng lại AiWorkerClient của worker upload
  → op="open", rồi op="next" cho từng frame
  → detector.infer(image, "upload")
  → engine.Observe(vị trí video theo giây, detections)
  → VideoAnalysisState.SetFrame
  → StageAsync các snapshot nguồn UPLOAD cần thiết
  → CompleteAsync lưu timeline
  → op="reopen_render", "step", "paint" để render theo decisions đã có
  → EncodeAsync tạo MP4 kết quả
  → đổi COMPLETED và dọn state RAM của job
~~~

Các trạng thái DB chính là UPLOADING → QUEUED → PROCESSING → RENDERING → COMPLETED, hoặc FAILED. Biến stage trong worker là mốc nội bộ chi tiết hơn và không đồng nhất từng bước với Status trong DB.

Python có các operation open, next, capture, reopen_render, step, paint, close cho luồng file. LIVE dùng infer/render trên JPEG nhận từ camera.

VideoAnalysisState giữ decisions theo index/FPS và lưu timeline. Frontend có thể lấy cửa sổ metadata để phát trong lúc phân tích; khi kết quả hoàn tất có file đã render.

Nguồn UPLOAD dùng chung nguyên lý RiskEngine và ảnh-trước-DB, nhưng không phát popup/còi LIVE toàn cục và không tạo Incident khi ACK. VideoAnalysesController hiện có policy AdminOnly.

## 18. Các API nên biết khi đọc Network trong trình duyệt

| API | Ý nghĩa |
|---|---|
| GET /api/cameras | Danh sách card và trạng thái nguồn |
| GET /api/cameras/{id}/analysis | Metadata AI mới nhất |
| GET /api/cameras/{id}/analysis/frame?frameId=... | JPEG của frame còn được giữ trong state |
| GET /api/equipments/{id}/camera/views | Cấu hình WebRTC |
| POST /api/equipments/{id}/camera/views | Offer/answer để mở view |
| GET /api/equipments/{id}/camera/views/{viewId} | Theo dõi/giữ phiên xem |
| DELETE /api/equipments/{id}/camera/views/{viewId} | Đóng view |
| GET /api/alerts | Danh sách Warning, lọc nguồn/status/camera/job |
| GET /api/alerts/{id} | Chi tiết Warning |
| PATCH /api/alerts/{id}/acknowledge | ACK có expectedSeverity |
| PATCH /api/alerts/{id}/cancel | Hủy Warning, body có expectedSeverity |
| GET /api/safety-alerts/incidents | Lịch sử Incident |
| GET/PUT /api/thresholds/safety-monitoring | Xem/lưu ngưỡng |
| /hubs/notifications | Kết nối SignalR, không phải đường truyền video |
| POST /api/video-analyses | Tạo job upload |
| GET /api/video-analyses/{jobId} | Trạng thái job |
| GET /api/video-analyses/{jobId}/alerts | Cảnh báo của job |

CamerasController và subscription metadata camera dùng ADMIN. AlertsController dùng policy SafetyOperators. Không suy ra quyền truy cập toàn hệ thống chỉ từ việc một nút có hiện trên UI hay không.

## 19. FPS và độ trễ: đo đúng đại lượng

~~~text
FPS nguồn RTSP       = tốc độ publisher/camera phát hình
FPS trình duyệt      = số video frame nhận/giải mã/trình bày mỗi giây
FPS AI toàn vòng     = số lần Infer hoàn tất mỗi giây, tính cả các công việc xen giữa
Nhịp metadata       = số camera_analysis gửi/nhận mỗi giây
Nhịp canvas         = nhịp vẽ theo video frame, có thể là tọa độ nội suy
~~~

Ba camera demo trong cấu hình local tại lúc đối chiếu đặt fps=25. Đây là cấu hình phát, không bảo đảm model cũng xử lý đủ 25 quan sát mới/giây.

Một vòng LIVE gồm lấy frame, InferAsync, RiskEngine, có thể RenderAsync, cập nhật state, stage snapshot và delay 10 ms. InferenceMilliseconds chỉ đo InferAsync; RenderMilliseconds đo render; AnalysisMilliseconds đo từ capture/AI/risk/render đến trước state.Set, chưa bao gồm upload/loop delay. Không trường nào tự nó là FPS của toàn hệ thống.

Broadcaster gộp kết quả và đợi 75 ms, nên số push không phải số inference. Không lấy 1000 / InferenceMilliseconds làm FPS toàn hệ thống.

Popup có thêm thời gian chuẩn bị/upload ảnh và lưu DB sau khi engine đã đưa ra quyết định. Thời gian hold lên CRITICAL là điều kiện nghiệp vụ, khác độ trễ tính toán/network.

## 20. Gián đoạn, reset và retry

| Tình huống | Đường xử lý hiện tại |
|---|---|
| Camera mất dữ liệu | CameraWorker đổi trạng thái, đóng client và retry |
| Simulator RTSP nối lại | AnalysisSession mới; LIVE reset tracker/engine khi thấy đổi session |
| Python worker lỗi | RunCameraAsync khởi động lại worker và trạng thái tracking/risk của worker đó |
| Camera bị bỏ khỏi tập cần chạy | Hủy camera task, gỡ state, dọn worker registry |
| Decoder backlog/lỗi | Pump bị retire, CaptureCore thử tạo lại |
| Upload Cloudinary lỗi | Outbox retry/backoff, chưa ghi Warning mới thiếu ảnh |
| SignalR lỗi | Cảnh báo và danh sách khôi phục bằng HTTP polling |
| Người dùng đã xử lý trước | API trả conflict, không tạo Incident trùng |
| Video upload lỗi | Job FAILED và ghi thông báo theo stage |

Simulator hiện mở FFmpeg cho từng lượt video. Hết lượt thì đóng publisher rồi mở lượt mới; đây là nguyên nhân có gián đoạn tại seam và reset tracker. Cơ chế phát vòng không ngắt RTSP kèm tín hiệu đầu lượt mới là hướng đề xuất, chưa phải code đang chạy.

Mất quan sát không tự đổi một Warning đã lưu thành CANCELLED/ACKNOWLEDGED, không tự xác nhận phục hồi và không tự giải quyết Incident.

## 21. Hướng dẫn chạy và đọc log

Các ví dụ dưới đây giả định cấu hình .env, database, model và native tools đã có. Tham khảo [README simulator](../tools/camera-simulator/README.md) để chuẩn bị nguồn.

Mở các terminal riêng từ thư mục gốc dự án.

**Terminal 1 — phát nguồn demo:**

~~~powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/camera-simulator/start-simulator.ps1 -ConfigPath tools/camera-simulator/cameras.local.json -Environment Testing
~~~

Environment phải khớp trường environment trong file cấu hình. Nếu đang dùng cấu hình khác thì thay tham số tương ứng.

**Kiểm tra nguồn:**

~~~powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/camera-simulator/check-streams.ps1 -ConfigPath tools/camera-simulator/cameras.local.json
~~~

Check đọc được RTSP chỉ chứng minh publisher phát được; chưa chứng minh backend giải mã được cấu hình camera hoặc AI đã chạy.

**Terminal 2 — backend:**

~~~powershell
Set-Location backend/src/SmartPool.API
dotnet run --launch-profile https
~~~

**Terminal 3 — frontend:**

~~~powershell
Set-Location frontend/thanh-minh-smart-pool
npm.cmd run dev
~~~

**Dừng các nguồn demo:**

~~~powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/camera-simulator/stop-simulator.ps1
~~~

Script Stop hiện không nhận ConfigPath. Dừng publisher không có nghĩa dừng backend/frontend.

Các nhóm cấu hình cần tìm trong .env.example/appsettings:

| Nhóm | Nội dung |
|---|---|
| CameraSettings | FFmpeg, AllowedEndpoints, thời gian reconnect, độ phân giải JPEG |
| CameraSettings__KeyRingPath | Vị trí khóa giải mã cấu hình camera, cần giữ ổn định |
| SafetyMonitoring | Enabled, PythonPath, WorkerPath, ModelPath, FontPath, FfprobePath, StoragePath |
| SafetyMonitoring__MaxLiveCameras | Giới hạn model LIVE resident; mặc định 4, clamp 1–8 trong worker |
| SafetyMonitoring__LiveReconnectGraceSeconds | Giữ slot cho worker mất kết nối ngắn; mặc định 15 giây, ưu tiên camera đang Connected |
| SafetyMonitoring__LiveInferenceThreads | Ngân sách thread CPU, được chia theo giới hạn camera |
| SafetyMonitoring__InferenceProviders | Danh sách provider ưu tiên |
| SafetyMonitoring:Risk / bảng ngưỡng DB | Điều kiện WARNING, CRITICAL, recovery và cooldown |
| Cloudinary | Thông tin dịch vụ upload ảnh |

Tài liệu không chứa mật khẩu, JWT secret, thông tin đăng nhập camera hoặc nội dung keyring.

Log cần tìm:

- **Live AI worker ready**: process Python đã load/warm-up model; đếm số dòng không tự chứng minh số camera đang hoạt động vì worker có thể restart.
- **tracks changed**: thay đổi danh sách TrackId của một camera.
- **active risk boxes changed**: thay đổi decisions; rỗng không đồng nghĩa phục hồi đã được xác nhận.
- **decoder restarted / InputBacklog / FrameAlignment / DecodeError**: decoder có vấn đề cần đối chiếu; DecodeError tạo lại pump để không mang FIFO đã lệch sang ảnh sau.
- **Unable to restore camera**: kiểm tra cấu hình, allowlist và keyring.
- **saved with image ... processing ... observation age**: thời gian backend lưu ảnh/cảnh báo; chưa phải lúc popup được vẽ trên màn hình.
- **Snapshot ... deferred**: đang retry, kiểm tra ảnh và đường upload.

## 22. Thứ tự đặt breakpoint để lần theo code

| Thứ tự | Điểm dừng | Câu hỏi cần trả lời |
|---:|---|---|
| 1 | Program.Main, AddInfrastructureServices | Đọc đúng cấu hình và đăng ký đúng singleton chưa? |
| 2 | CameraSupervisor.ExecuteAsync | DB có camera Active và metadata mã hóa không? |
| 3 | CameraService.RestoreAsync | Giải mã được cấu hình và qua policy không? |
| 4 | CameraWorker callback ReceivedData | Có dữ liệu, codec và RTP timestamp không? |
| 5 | CameraAnalysisFramePump callback JPEG | Ảnh và timestamp đang được ghép như thế nào? |
| 6 | CameraFrameSource.CaptureCore | Latest có mới, pump có bị restart không? |
| 7 | LiveMonitoringWorker.RunCameraAsync | Camera này có được chọn và có process Python chưa? |
| 8 | Detector.infer / AiWorkerClient.Detections | TrackId, confidence và box thực tế là gì? |
| 9 | RiskDecisionEngine.Observe | Tại sao nhận/loại quan sát, lên mức hoặc giữ mức? |
| 10 | CameraAnalysisState.Set / broadcaster | FrameId/RTP nào thực sự được gửi? |
| 11 | CameraRiskFrameBuffer.sample / drawBoxes | Mẫu nào được chọn cho frame video này? |
| 12 | TryStage → StageAsync → ProcessOneOutboxAsync | Ảnh có đang chờ hoặc bị backlog không? |
| 13 | PrepareSnapshotAsync / RecordAsync | Tạo, nâng mức hay bỏ qua do dedup? |
| 14 | useGlobalSafetyAlerts.handle / resync | Popup nhận push hay được khôi phục qua polling? |
| 15 | HandleAsync | expectedSeverity có khớp DB khi đã khóa chưa? |

Đặt breakpoint lâu trong RTSP/AI có thể làm nguồn timeout, queue đầy hoặc metadata hết hạn. Khi đo thời gian thực nên dùng log có giới hạn hoặc tracepoint thay vì dừng mọi thread nhiều giây.

## 23. Phân biệt hiện trạng và các hướng sửa chưa triển khai

| Nội dung | Trạng thái ở lần đối chiếu |
|---|---|
| Video nguồn tách khỏi nhịp inference khi dùng smooth | Đã có |
| Box nội suy theo RTP, không còn EMA 80/110 ms | Đã có |
| Giữ box theo geometry thật với active IDs; thiếu geometry chỉ giữ tối đa 300 ms | Đã có |
| Warning có ảnh trước DB, upload đồng thời giới hạn | Đã có |
| ACK và CANCEL kiểm tra expectedSeverity | Đã có |
| Geometry hiển thị độc lập với nhịp RiskDecision, sau lần chốt risk đầu tiên | Đã có cache mức nguy cơ + geometry theo track; không đổi quy tắc lưu bằng chứng AI |
| Kiểm định pixel/RTP trên H.264 baseline và lỗi picture | Đã có test nội dung; decoder lỗi được restart. Chưa có PTS xuyên container hoặc bảo đảm mọi B-frame/H.265 |
| Simulator loop không ngắt RTSP, reset AI qua dấu đầu lượt riêng | Chưa có |
| Đánh giá độ chính xác model với nhãn thủ công từng frame | Không được suy ra từ các phép đo render hiện có |

Các công cụ kiểm tra ở tools/camera-simulator:

- [test-live-playback.mjs](../tools/camera-simulator/test-live-playback.mjs): Chrome, video/box/RTP và trace trên API test.
- [analyze-box-continuity.mjs](../tools/camera-simulator/analyze-box-continuity.mjs): phân tích trace; sai lệch so với tọa độ AI không phải ground truth của người.
- [test-alert-popups.mjs](../tools/camera-simulator/test-alert-popups.mjs): nhiều popup, timeout, xử lý lỗi và xác nhận bị nâng mức; dùng API/SignalR fixture.
- [VERIFICATION.md](../tools/camera-simulator/VERIFICATION.md): kết quả và giới hạn của các lần kiểm tra.

## 24. Lộ trình đọc ngắn nếu chưa muốn đọc hết

1. Đọc Program.Main và phần đăng ký SafetyMonitoring trong DependencyInjection.
2. Đọc LiveMonitoringWorker.RunCameraAsync để thấy toàn bộ một vòng xử lý LIVE.
3. Đi vào AiWorkerClient.InferAsync rồi Detector.infer của Python.
4. Đọc RiskDecisionEngine.Observe để hiểu chính xác điều kiện cảnh báo.
5. Đi theo CameraAnalysisState.Set sang broadcaster, useCameraRisks và CameraRiskFrameBuffer.sample.
6. Đi theo snapshots.TryStage sang SnapshotUploadWorker và SafetyMonitoringService.RecordAsync.
7. Đi theo AlertActionButtons.handle sang AlertsController và SafetyMonitoringService.HandleAsync.
8. Đọc VideoAnalysisWorker sau cùng để so sánh LIVE với phân tích file.

Tại mỗi bước, ghi lại **đầu vào → đầu ra → nơi lưu trạng thái → caller kế tiếp**. Cách đọc này giúp tách được thời gian thực, trạng thái trong RAM, hàng đợi trên đĩa và giao dịch database.
