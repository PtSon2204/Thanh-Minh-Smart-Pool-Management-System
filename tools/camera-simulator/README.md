# Camera RTSP Simulator — Smart Pool

Biến MP4 cục bộ thành nguồn camera LIVE: FFmpeg → MediaMTX → CameraService → WebRTC và LiveMonitoringWorker → ONNX YOLO11 → ByteTrack → RiskDecisionEngine → snapshot/upload → LIVE Alert → SignalR. Không gọi API upload video, không tạo VideoAnalysisJob, không tạo cảnh báo giả.

Chỉ dùng với API và database Development/Testing riêng. Không tự seed, sửa hoặc dừng camera thật. Bộ công cụ không tự khởi động cùng ứng dụng. `enabled` mặc định `false`; script yêu cầu `-Environment`. Backend từ chối khởi động nếu endpoint `Simulator=true` xuất hiện ngoài Development/Testing.

## 1. Chuẩn bị trên Windows 10/11

- Windows PowerShell 5.1 hoặc PowerShell 7 trên Windows.
- Cài [FFmpeg/FFprobe](https://ffmpeg.org/download.html), kiểm tra `ffmpeg -version`, `ffprobe -version`. Bản FFmpeg phải có `libx264`.
- Tải [MediaMTX v1.21.3 cho Windows amd64](https://github.com/bluenviron/mediamtx/releases/download/v1.21.3/mediamtx_v1.21.3_windows_amd64.zip), giải nén vào `tools/camera-simulator/bin/` hoặc thêm vào PATH. Bộ cấu hình được đối chiếu với [tài liệu MediaMTX](https://mediamtx.org/docs/references/configuration-file).
- Giữ nguyên model `model/best.onnx`; cài dependency theo `backend/ai/requirements.txt`. Chạy `python backend/ai/smoke_test.py` để kiểm tra ONNX, tracker và renderer.
- Chuẩn bị video của bạn. Không tải video tự động. Không thêm MP4, binary, log hay secrets vào git; thư mục videos, bin và .runtime đã được ignore.

Chạy từ gốc repository; nếu PowerShell chặn script, chỉ bỏ chặn cho lần chạy bằng `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ...`. Không cần đổi execution policy toàn máy.

## 2. Cấu hình và chạy

```powershell
Copy-Item tools/camera-simulator/cameras.json tools/camera-simulator/cameras.local.json
```

Sửa `cameras.local.json`:

- `enabled: true`, `environment: "Development"` hoặc `"Testing"`.
- `mediaMtxPath: "./bin/mediamtx.exe"` nếu không dùng PATH.
- Mỗi camera: tên, `streamName`, `videoPath`, `fps` (1–60), `loop`, `enabled`.
- Chọn **video gốc chưa vẽ box AI**. Không dùng file kết quả `*-result.mp4` của SmartPool làm đầu vào: box đã nằm trong pixel video sẽ chồng lên box LIVE. Start từ chối file kết quả có định dạng UUID của SmartPool khi tìm thấy bản gốc cùng thư mục; hãy chọn bản `<UUID>.mp4`.
- Đường dẫn video/tool có dấu cách được hỗ trợ, đường dẫn tương đối tính từ **thư mục chứa JSON**, không phụ thuộc thư mục chạy lệnh. Tránh đổi file JSON khi publisher đang chạy; Stop rồi Start để áp cấu hình mới.
- `publishBaseUrl`: địa chỉ FFmpeg dùng để publish; mặc định `rtsp://127.0.0.1:8554`.
- `consumerBaseUrl`: địa chỉ backend dùng; chỉ để hiển thị/check, không thay `publishBaseUrl`.
- `loopPauseSeconds`: thời gian nghỉ giữa các lượt video (0–60 giây), mặc định **0** để phát lại ngay sau EOF; không phải ngưỡng risk.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/camera-simulator/start-simulator.ps1 -ConfigPath tools/camera-simulator/cameras.local.json -Environment Development
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/camera-simulator/check-streams.ps1 -ConfigPath tools/camera-simulator/cameras.local.json
```

Đầu ra mong đợi: `CONNECTED rtsp://127.0.0.1:8554/pool1 | h264 | ... | FPS 25/1`. Start xác nhận process đã mở; **Check mới kiểm tra media**. Có thể cần đợi một GOP (khoảng nửa giây ở 25 FPS). Nếu vừa kết thúc một lượt loop, chạy Check lại sau khi publisher mở lượt tiếp theo.

MediaMTX mặc định chỉ bind loopback, TCP, không xác thực; chỉ cho đọc/publish từ loopback. API/RTMP/HLS/WebRTC của MediaMTX đều tắt. Live Preview vẫn dùng WebRTC sẵn có của SmartPool. `overridePublisher: no` ngăn ghi đè nguồn đang phát.

Nếu dùng MediaMTX do bạn quản lý (native/Docker/máy test khác), thêm `-UseExistingServer`. Script không quản lý/dừng server đó. Không có Compose mới vì repository không có stack Docker cần tích hợp; có thể tự chạy image `bluenviron/mediamtx:1.21.3` với cấu hình test riêng, publish cổng TCP và dùng `-UseExistingServer`.

### Loop và timestamp

FFmpeg dùng `-re`, H.264 baseline/yuv420p, tối đa 1280×720 giữ tỉ lệ, không upscale, không audio, không B frame, GOP khoảng nửa giây, TCP, 2 thread encoder. Với cấu hình 720p/25 FPS, nguồn tương thích đường passthrough WebRTC của backend, tránh encode lại xuống 15 FPS. FPS/resolution cao hơn giới hạn passthrough vẫn có thể cần chuyển mã. Không tạo pipeline inference thứ hai. Tham khảo [cách FFmpeg điều tiết realtime](https://ffmpeg.org/ffmpeg.html).

Khi đổi cấu hình encoder/script, Stop rồi Start lại publisher để áp dụng cho phiên FFmpeg mới. Kalman chỉ dùng nối danh tính; vị trí box lấy từ detection thực tế.

**Camera AI đồng bộ hình/box:** với H.264 baseline tương thích (bao gồm ba camera demo), danh sách camera và cửa sổ phóng to dùng `analysis: true, smooth: true`. Video gốc đi thẳng qua WebRTC theo FPS nguồn; AI chạy độc lập. Metadata chứa RTP timestamp của frame đã phân tích; canvas lấy timestamp của frame trình duyệt đang hiển thị để nội suy vị trí giữa hai detection cùng track. Severity/episode lấy từ quan sát quá khứ, không suy đoán mức cảnh báo. Buffer trình duyệt yêu cầu khoảng 400 ms để đợi metadata; box quá hạn hoặc không đủ dữ liệu được ẩn. Trình duyệt thiếu RTP timestamp hoặc nguồn không tương thích dùng `CameraAnalysisVideoSource` vẽ sẵn box trên đúng frame, FPS phụ thuộc AI. Snapshot lưu database vẫn render từ frame AI thực tế. Không tạo thêm model hoặc kết nối RTSP cho người xem. Video kết quả tải lên cũ giữ nguyên pixel.

**Độ bám box:** player không lọc vị trí theo box đã vẽ ở frame trước. Khi đủ điểm AI trước/sau, nội suy cubic đơn điệu theo RTP để chuyển động liền mạch mà vẫn đi qua đúng tọa độ đã đo; chỉ có hai điểm thì nội suy tuyến tính. Giữ tâm/kích thước trong giới hạn quan sát và vùng ảnh, không kéo box chạy đuổi theo người bằng EMA. Đây là đồng bộ tọa độ hiển thị; độ chính xác nhận diện vẫn phụ thuộc model và video nguồn.

**Box liên tục khi metadata đến trễ:** giữ quan sát hiện tại tối đa 600 ms theo RTP thay cho ngưỡng 100 ms ngắn hơn chu kỳ push 200 ms trước đây (hiện gửi mỗi 75 ms nếu có frame mới). Nếu chưa có điểm nội suy kế tiếp, dùng chuyển động tâm box giữa hai quan sát cùng track để dự đoán tối đa 200 ms, giữ nguyên kích thước rồi chờ cập nhật. Backend xác nhận track hết nguy cơ thì xóa box ngay; frame thiếu confidence giữ box tối đa 300 ms nếu activeRiskTrackIds vẫn chứa track; dữ liệu AI quá một giây, đổi tracking session hoặc mất kết nối vẫn xóa box. Không đổi thuật toán severity, điều kiện cảnh báo hoặc ảnh lưu.

**Giảm giật khi mở nhiều camera:** mỗi camera đang giám sát có một tiến trình AI và tracker riêng, dùng chung kết nối RTSP hiện có. Camera chậm hoặc worker lỗi không chặn/reset các camera còn lại. Số model trong bộ nhớ bị giới hạn bởi `SafetyMonitoring__MaxLiveCameras` (mặc định 4, tối đa 8); CPU fallback chia ngân sách `LiveInferenceThreads` theo giới hạn này. Cách này dùng thêm RAM/VRAM so với một worker tuần tự. Truyền JPEG giữa C# và Python bằng binary; decoder liên tục đồng thời cập nhật preview/health, tránh mở FFmpeg định kỳ. Gửi metadata và ghi snapshot outbox chạy ở worker riêng. Nguồn fallback paired dùng GOP 12, CRF 18, tối đa 12 frame pending và giới hạn tuổi backlog. Font nhãn được cache. Khởi động lại backend và tải lại trang để áp dụng.

**Độ nét Camera AI:** frame trước inference/render mặc định giữ tối đa 1280×720, đúng tỉ lệ và không upscale; JPEG qscale=2 thay cho ảnh 640 px/qscale=5 trước đây. Model vẫn resize tensor về kích thước riêng của model, còn preview/snapshot giữ frame lớn hơn. Có thể cấu hình `CameraSettings__AnalysisMaxWidth`, `CameraSettings__AnalysisMaxHeight`, `CameraSettings__AnalysisJpegQscale` trong backend (xem `.env.example`); khởi động lại backend để áp dụng. Nguồn simulator hiện giới hạn 720p nên đặt AI preview cao hơn không tạo thêm chi tiết từ nguồn đó.

**Loop cố ý có ngắt kết nối:** mỗi lượt chạy FFmpeg một lần rồi đóng publisher; supervisor nghỉ `loopPauseSeconds` và mở lượt mới. Vì vậy không dùng `-stream_loop -1` nối hai cảnh khác nhau vào cùng tracker. Mỗi RTSP reconnect trên endpoint simulator tạo `AnalysisSession` mới; decoder, ByteTrack và RiskDecisionEngine của camera đó được reset khi nhận frame phiên mới. Camera thật giữ nguyên danh tính và cơ chế risk hiện có. Mất quan sát không ghi NORMAL, không tự đóng cảnh báo đã lưu. Lượt replay mới có thể tạo episode mới thật; xử lý các cảnh báo test trước khi chạy một đợt mới.

**Giảm thời gian nối lại:** mặc định không nghỉ giữa hai lượt (`loopPauseSeconds: 0`). Backend retry RTSP simulator sau 250 ms thay vì lịch camera vật lý (mặc định 5 giây); có thể chỉnh `CameraSettings__SimulatorReconnectDelayMilliseconds` trong khoảng 100–2000 ms. Decoder AI của simulator thử lại sau 100 ms. API trả cờ `fastReconnect` cho camera simulator để player WebRTC dùng retry nhanh có giới hạn và danh sách camera cập nhật mỗi giây, tránh chờ 10 giây khi card vừa hiện offline. Camera vật lý vẫn dùng lịch cũ. GOP nửa giây giúp consumer sớm bắt được keyframe sau khi nối lại. Vẫn có ngắt ngắn do mở lại FFmpeg/RTSP/WebRTC; đây không phải phát vòng liên tục không gián đoạn. Khởi động lại backend và Stop/Start simulator để áp dụng code/cấu hình mới.

Timestamp LIVE lấy từ thời điểm camera/frame được nhận ở host, không dùng ngày giờ/PTS gốc của MP4. PTS của FFmpeg chỉ dùng trong từng phiên media. Video quá ngắn có thể chưa đủ thời gian nhận diện, reconnect và đạt hold; dùng clip dài đủ để quan sát model, không giảm ngưỡng để ép có cảnh báo.

## 3. Thêm camera qua quản lý thiết bị hiện tại

Khởi động API với môi trường Development/Testing, DB test riêng và cấu hình camera/AI đã có. Thêm một **index mới chưa dùng** vào allowlist `.env` của API; không ghi đè endpoint của camera thật:

```dotenv
# Ví dụ index 0 CHỈ khi chưa có endpoint nào; nếu đã có, dùng index tiếp theo.
CameraSettings__AllowedEndpoints__0__Host=127.0.0.1
CameraSettings__AllowedEndpoints__0__Port=8554
CameraSettings__AllowedEndpoints__0__Simulator=true
CameraSettings__FfmpegPath=C:/path/to/ffmpeg.exe
```

Không bật `Simulator` cho endpoint chứa camera thật. Đặt cấu hình trước lần tạo camera simulator đầu tiên, không đổi cờ này trên camera đã đăng ký. Cờ phân biệt danh tính theo RTSP path, cho phép pool1/pool2/pool3 cùng host/port; camera vật lý cũ giữ identity theo host/port như trước.

Giữ nguyên `SafetyMonitoring__Enabled`, `PythonPath`, `WorkerPath`, `ModelPath`, `FontPath`, `FfprobePath`, Cloudinary và WebRTC của hệ thống; xem `.env.example`. Các ngưỡng được `RiskThresholds` đọc từ DB, fallback cấu hình hệ thống; simulator không chứa/copy/hardcode ngưỡng. Xem giá trị hiệu lực bằng UI cấu hình ngưỡng hoặc `GET /api/thresholds/safety-monitoring` với quyền ADMIN.

1. Đăng nhập ADMIN → Quản lý thiết bị → thêm nhóm **Camera**, nguồn **RTSP**.
2. Tên: tùy chọn; URL: `rtsp://127.0.0.1:8554/pool2`; để trống username/password.
3. Chọn/nhập khu vực test theo quản lý hiện tại; hệ thống hiện lưu khu vực camera bằng `Equipment.Location`, không có Camera/Area entity mới cho simulator.
4. Lưu. API hiện tại thực sự kết nối/giải mã frame trước khi cấp ID và persist metadata đã mã hóa. URL không kết nối được sẽ không được coi là camera thành công.
5. Camera Active được supervisor/AI worker tự giám sát (không có nút Start AI riêng). Đổi trạng thái khỏi Active bằng nghiệp vụ thiết bị để ngừng giám sát; Stop publisher mô phỏng mất kết nối, không đổi trạng thái thiết bị trong DB.
6. Vào danh sách camera/phóng to để xem WebRTC và khung AI. Xem `GET /api/cameras/{id}/analysis` để kiểm tra detections, trackId, observedAt, frameId và thời gian inference.

Không seed tự động; không sửa camera thật; không hardcode CameraId. Trong VLC: Media → Open Network Stream → nhập URL RTSP (dùng TCP nếu cần). VLC thành công chỉ chứng minh media, chưa chứng minh AI/alert.

### Host, Docker, LAN

| Consumer | Địa chỉ thường dùng |
|---|---|
| Backend trên cùng Windows host | `rtsp://127.0.0.1:8554/pool1` |
| Backend trong Docker Desktop | `rtsp://host.docker.internal:8554/pool1` nếu Docker route tới host hỗ trợ |
| Backend ở máy LAN khác | `rtsp://<IP LAN máy MediaMTX>:8554/pool1` |

Với Docker/LAN, tạo `mediamtx.local.yml`, bind RTSP vào interface phù hợp và giới hạn `authInternalUsers[].ips` chỉ cho publisher/consumer test. Mở firewall TCP đúng cổng, đúng subnet test; không công khai server không xác thực ra Internet. `consumerBaseUrl` chỉ đổi địa chỉ hiển thị, **không** tự mở firewall/bind server. Nếu URL dùng hostname, allowlist backend phải có `Addresses` pin tất cả IP hostname phân giải ra, theo `CameraAddressPolicy` hiện tại. Không tắt policy để bỏ qua lỗi AddressNotAllowed. Với backend Linux, giữ cấu hình bảo vệ data-protection key bằng chứng chỉ như hệ thống yêu cầu.

## 4. Popup, snapshot, xác nhận và âm thanh

- Cảnh báo phải do model và RiskDecisionEngine quyết định. Tên clip drowning không bảo đảm đạt WARNING/CRITICAL.
- LIVE có CameraId thật; snapshot được stage từ đúng frame vào outbox trên đĩa. SnapshotUploadWorker vẽ khung, upload thành công rồi mới ghi cảnh báo và phát SignalR. Áp dụng cả LIVE và UPLOAD; bản ghi mới luôn có ImageUrl và SnapshotStatus=COMPLETED. Cloudinary lỗi thì giữ tác vụ trên đĩa để retry, chưa ghi cảnh báo vào DB; video/inference tiếp tục chạy. Outbox mặc định tối đa 256 tác vụ (`SafetyMonitoring__MaxPendingAlertSnapshots`).
- Popup toàn cục chỉ nhận LIVE, theo quyền ADMIN/LIFEGUARD đang có; hiển thị tên camera, Equipment.Location, mức độ, thời điểm và ảnh. “Xem chi tiết” trên popup mở và làm nổi bật camera tương ứng, kể cả đang ở trang camera. “Xem chi tiết” tại Cảnh báo camera mở ảnh chụp lúc phát hiện cảnh báo. Chi tiết video camera hiện chỉ ADMIN xem được theo quyền hiện tại.
- WARNING acknowledge không tạo Incident. CRITICAL acknowledge tạo Incident trong transaction/row lock; request trùng nhận 409. Cancel không tạo Incident.
- TTS đã chuyển sang `incident_created` sau acknowledge, có fallback từ HTTP thành công của client xác nhận, dedupe theo IncidentId trong tab. Câu đọc: “Sự cố khẩn cấp xảy ra ở khu vực {location}.” Không đọc lại khi snapshot cập nhật, không đọc WARNING/UPLOAD/CANCELLED hay CRITICAL pending. Nếu mất event trên client khác thì không đọc bù lịch sử.
- Còi cảnh báo hiện có là cơ chế riêng với giọng đọc. Khi test, dùng frontend im lặng:

```powershell
# Chạy trong frontend/thanh-minh-smart-pool
npm.cmd run dev -- --mode camera-test
# Hoặc npm.cmd run build -- --mode camera-test
```

Mode `camera-test` tắt cả còi và TTS mặc định. Chỉ trên client/thiết bị test chỉ định, đặt `VITE_SAFETY_TEST_AUDIO=true` rồi khởi động/build lại để thử âm thanh. Không mở client vận hành production nối vào DB test. Không có lệnh điều khiển loa công cộng trong simulator.

**Phân quyền hiện tại là theo vai trò, không theo từng camera/khu vực.** Simulator giữ chính sách đó; chưa có bảng phân công quyền khu vực để lọc thêm. Nếu cần cô lập người nhận theo khu vực, dùng instance/DB test riêng; không dùng camera test trên instance chung với nhân viên vận hành rồi kỳ vọng lọc khu vực.

## 5. Dừng, reconnect và xử lý lỗi

```powershell
# Dừng một nguồn, giữ server và các camera khác
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/camera-simulator/stop-simulator.ps1 -StreamName pool2
# Khôi phục nguồn đó
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/camera-simulator/start-simulator.ps1 -ConfigPath tools/camera-simulator/cameras.local.json -Environment Development -StreamName pool2
# Dừng tất cả nguồn và chỉ MediaMTX do simulator sở hữu
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/camera-simulator/stop-simulator.ps1
```

Stop dùng PID + thời điểm khởi động + executable để xác minh quyền sở hữu; dừng cả process con của shim bằng `taskkill /T`, không kill mọi FFmpeg theo tên. Stop đặt marker trước khi kill để supervisor không khởi động lại. Nếu chạy trong sandbox chặn process tree, cần cho phép lệnh Stop ngoài sandbox; không xóa PID khi chưa dừng được. Giữ `.runtime` đến khi dọn xong. Không xóa PID file thủ công khi process còn chạy.

| Hiện tượng | Kiểm tra |
|---|---|
| Thiếu tool/video | Đường dẫn trong JSON, PATH, enabled của từng camera; Start kiểm tra trước khi mở server |
| Cổng bận | Không chiếm server khác; đổi port đồng bộ JSON/YAML/backend hoặc dùng `-UseExistingServer` cho server test đã biết |
| MediaMTX không chạy | `.runtime/mediamtx.stderr.log`; YAML phải UTF-8 **không BOM**, phiên bản tương thích |
| Stream 404 | Publisher chưa chạy/đã EOF/đang nghỉ loop; xem `publisher-*.stderr.log`, `ffmpeg-*.stderr.log` |
| FFmpeg crash | Supervisor dừng và báo lỗi, không retry vô hạn; sửa lỗi rồi Stop/Start stream đó |
| AI chưa cấu hình | `SafetyMonitoring` paths, dependencies, model, môi trường API; Check RTSP không thay kiểm tra AI |
| UI offline | RTSP reconnect có retry delay; không coi frame cũ là LIVE, chờ frame mới; kiểm tra analysis state/observedAt |
| WebRTC không kết nối | HTTPS khi triển khai, ICE candidates, BindAddress, dải UDP WebRTC/TURN của **backend**; port RTSP không thay thế ICE |
| Cloudinary lỗi | Credentials test riêng, SnapshotStatus/retry; không tạo cảnh báo giả để kiểm thử |

MediaMTX restart: Stop toàn bộ rồi Start; hoặc nếu server do bạn quản lý thì restart server đó và khởi động lại publisher đã lỗi. Network interruption: chặn kết nối trên **môi trường test** rồi khôi phục; không sửa firewall của camera thật. `loop:false` mô phỏng EOF tự nhiên. FFmpeg crash có thể dùng PID được xác minh từ `.runtime/ffmpeg-<stream>.json` (Stop từng stream là phép thử mất kết nối an toàn hơn).

## 6. Checklist nghiệm thu

Các điểm tích hợp đã đối chiếu trong repository (đường dẫn dưới `backend/src/` trừ khi ghi khác):

| Chức năng | Code hiện có được tái sử dụng |
|---|---|
| Camera API, CRUD, xem stream | `SmartPool.API/Controllers/{EquipmentsController,CamerasController}.cs`; `SmartPool.Application/Features/ManageEquipments/` |
| Entity và cấu hình camera | `SmartPool.Domain/Entities/Equipment.cs`; `CameraMetadata`, `CameraConnectionConfiguration`; `SmartPool.Infrastructure/Cameras/CameraOptions.cs` |
| RTSP connection/retry/allowlist | `SmartPool.Infrastructure/Cameras/{CameraService,CameraWorker,CameraAddressPolicy,CameraSupervisor}.cs` |
| WebRTC và frame AI | `SmartPool.Infrastructure/Cameras/{CameraStreamService,CameraWebRtcSource,CameraAnalysisVideoSource,CameraAnalysisFramePump}.cs` |
| Worker LIVE | `SmartPool.Infrastructure/SafetyMonitoring/{LiveMonitoringWorker,CameraFrameSource,AiWorkerClient}.cs` |
| YOLO11/ByteTrack | `backend/ai/worker.py` — ONNX hiện tại, tracker theo camera key |
| Risk/window/ngưỡng | `SmartPool.Application/Features/SafetyMonitoring/RiskDecisionEngine.cs`; `SmartPool.Infrastructure/SafetyMonitoring/RiskThresholds.cs` |
| Persist LIVE và acknowledge/incident | `SmartPool.Infrastructure/Persistence/Repositories/MonitoringRepository.cs`; `SmartPool.API/Controllers/AlertsController.cs` |
| Snapshot/Cloudinary | `SmartPool.Infrastructure/SafetyMonitoring/SnapshotUploadWorker.cs`; `backend/ai/snapshot.py` |
| SignalR/nhóm quyền | `SmartPool.Infrastructure/Services/{NotificationHubBase,SignalRNotificationService}.cs` |
| Popup và voice | `frontend/thanh-minh-smart-pool/src/features/safety/components/{GlobalSafetyAlertsHost,GlobalAlertPopup}.jsx`; `hooks/useGlobalSafetyAlerts.js`, `hooks/useSafetyTts.js` |

| Test | Thao tác và kết quả cần ghi nhận |
|---|---|
| Normal | Video normal thật → CONNECTED/WebRTC/track; chỉ không cảnh báo nếu model/risk không đạt ngưỡng |
| Nguy cơ | Video phù hợp → quan sát confidence/track/ratio; nếu đủ ngưỡng thì LIVE WARNING/CRITICAL, không có VideoAnalysisJob |
| Snapshot | So ảnh đúng frame/box cam #F97316 hoặc đỏ #EF4444; chỉ ghi cảnh báo sau upload; Cloudinary lỗi giữ outbox để retry |
| Critical ACK | Trước ACK chưa có Incident/TTS; ACK thành công tạo đúng 1 Incident/camera/location; lặp ACK 409 và không đọc lặp |
| Warning ACK / Cancel | Trạng thái đúng; không Incident/giọng CRITICAL |
| Offline / EOF / crash | Stop một stream, `loop:false`, crash publisher → UI offline, frame/box cũ hết hạn, không thêm alert từ frame cũ |
| Multi-camera | Hai ID thật, nhiều RTSP paths, track/window/episode và nguồn popup tách biệt |
| Reconnect / restart / network | Khôi phục stream/server → frame mới, session simulator mới, không dùng window cũ |
| Quyền | ADMIN/LIFEGUARD theo policy hiện có nhận LIVE, SALES/CUSTOMER không nhận; chi tiết camera vẫn ADMIN |
| Production | `enabled:false` mặc định; backend Production + Simulator=true bị validation từ chối; không seed/auto-start |

Chạy test mã nguồn:

```powershell
dotnet test backend/tests/SmartPool.UnitTests/SmartPool.UnitTests.csproj --filter 'FullyQualifiedName~CameraEquipmentTests|FullyQualifiedName~RiskDecisionEngineTests|FullyQualifiedName~SafetyIncidentFlowTests'
dotnet test backend/tests/SmartPool.IntegrationTests/SmartPool.IntegrationTests.csproj --filter 'FullyQualifiedName~CameraContinuousAnalysisTests'
```

Test acceptance opt-in `SimulatorRtsp_ExercisesExistingLivePipeline_WithRealModelAndDatabase` tạo DB `smartpool_test_<GUID>` riêng qua fixture, tạo camera bằng HTTP API thật, nhận frame WebRTC, chạy LiveMonitoringWorker/model, quan sát analysis và LIVE alerts/SignalR. ACK/cancel chỉ chạy nếu model thực sự phát mức tương ứng; báo cáo ghi rõ nhánh nào chưa được thử. Không phát âm thanh vì không mở trình duyệt. Cần PostgreSQL local cho test và Redis theo cấu hình integration hiện có; tuyệt đối không trỏ `ConnectionStrings__DefaultConnection` vào DB vận hành.

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5433;Database=smartpool_test;Username=postgres;Password=<test-only>'
$env:SMARTPOOL_SIMULATOR_TEST_URL = 'rtsp://127.0.0.1:8554/pool2'
# Tùy chọn: kiểm tra camera thứ hai trên cùng host/port và cancel cảnh báo thực của nguồn đó.
$env:SMARTPOOL_SIMULATOR_TEST_SECOND_URL = 'rtsp://127.0.0.1:8554/pool1'
$env:SMARTPOOL_SAFETY_TEST_PYTHON = (Get-Command python.exe).Source
$env:SMARTPOOL_CAMERA_TEST_FFMPEG = (Get-Command ffmpeg.exe).Source
$env:SMARTPOOL_SAFETY_TEST_FFPROBE = (Get-Command ffprobe.exe).Source
$env:SMARTPOOL_SAFETY_TEST_FONT = 'C:/Windows/Fonts/arial.ttf'
dotnet test backend/tests/SmartPool.IntegrationTests/SmartPool.IntegrationTests.csproj --filter 'FullyQualifiedName~SimulatorRtsp_ExercisesExistingLivePipeline'
```

Kết quả cục bộ: `.runtime/live-acceptance.json`. `observedFramesPerSecond` là tốc độ frame AI **quan sát được qua polling**, có thể thấp hơn inference FPS thật. `meanInferenceMs` là thời gian inference, `maxObservationAgeMs` gồm tuổi frame lúc polling; không coi là latency WebRTC end-to-end. Source FPS lấy từ Check. Dropped frames và queue length chưa có telemetry xuất ra trong hệ thống hiện tại; ghi “chưa đo”, không tự suy ra bằng source FPS trừ inference FPS. Nếu tuổi frame tăng liên tục thì không kết luận realtime. Đọc [VERIFICATION.md](VERIFICATION.md) để biết phần đã/chưa kiểm chứng trong lần triển khai này.

### Kiểm tra nhiều popup trong trình duyệt

```powershell
node tools/camera-simulator/test-alert-popups.mjs
```

Script mở Chrome headless và Vite riêng ở cổng trống, dùng mode `camera-test` im lặng. Cần Chrome, Playwright Core (mặc định tại `backend/.test-runtime/ui-test/node_modules/playwright-core/index.mjs`, hoặc đặt `SMARTPOOL_PLAYWRIGHT_MODULE` tới file module tương ứng), và `backend/.test-runtime/jobs/**/fixture.mp4` đã tạo từ bài integration video native. API/SignalR được mock hoàn toàn, không cần backend đang chạy và không ghi cảnh báo/Incident vào database.

Mỗi viewport 1440×900, 320×740, 844×390 thử 10 popup LIVE cùng đợt, sự kiện trùng/nâng mức, thứ tự, ảnh, cuộn, hai xác nhận đồng thời, API lỗi/thử lại, đóng/hủy/chi tiết theo đúng cảnh báo, timeout riêng 30 giây và polling không làm hiện lại popup đã đóng. Kiểm tra thêm hai popup video với fixture phát thật. Đồng hồ trình duyệt được điều khiển để thao tác/chụp ảnh không làm sai phép đo timeout. Báo cáo và ảnh nằm ở `backend/.test-runtime/popup-burst/`. Kiểm tra tạo Incident thật vẫn thuộc integration API/database, không suy ra từ response mock của bài UI.
