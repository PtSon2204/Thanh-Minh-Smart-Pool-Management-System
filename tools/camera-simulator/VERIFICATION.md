# Kết quả kiểm chứng triển khai — 12/11/2026

## Môi trường và phạm vi

Kiểm thử trên Windows/Windows PowerShell 5.1, FFmpeg/FFprobe có sẵn, MediaMTX v1.21.3 tải từ release chính thức, model `model/best.onnx` và dependency Python hiện có. Dùng hai MP4 cục bộ chưa gắn nhãn từ dữ liệu test có sẵn; không tải video, không commit video/binary.

MediaMTX test bind `127.0.0.1:18554`, tách khỏi cổng mặc định 8554. PostgreSQL test được tạo riêng dưới `backend/.test-runtime/camera-simulator-pg`, cổng 15433; fixture tạo/xóa database `smartpool_test_<GUID>` cho từng lượt acceptance. Không đổi `.env`, không sử dụng database của API đang chạy. Build .NET vào `backend/.test-runtime/camera-simulator-build` vì DLL mặc định đang được API đang chạy giữ; không dừng API đó.

## Đã chạy và đạt

| Kiểm tra | Bằng chứng |
|---|---|
| Publisher nhiều nguồn | Check hai path pool1/pool2: H.264, 1280×720, 25/1 FPS |
| Loop | Publisher tiếp tục chạy qua nhiều lượt; PID FFmpeg thay đổi ở mỗi lượt, log publisher không có lỗi ở lượt kiểm tra cuối |
| Dừng riêng camera | Stop pool1 → Check pool1 DISCONNECTED, pool2 vẫn CONNECTED; Start riêng pool1 hoạt động trở lại |
| Connection/reconnect/session | Hai integration tests `CameraContinuousAnalysisTests` đạt: capture liên tục; nhiều path cùng host/port, reconnect đổi AnalysisSession |
| Camera Management thực | Acceptance tạo hai camera bằng `POST /api/equipments`, không xác thực RTSP, ID thật trong database test |
| WebRTC thực | Peer nhận được H.264 từ `CameraStreamService` trên camera MediaMTX thật |
| YOLO11/ByteTrack/Risk LIVE | `LiveMonitoringWorker` thực xử lý nguồn RTSP, nhận track, tạo LIVE alert; không gọi pipeline upload |
| Ngưỡng DB | Acceptance đọc ngưỡng hiệu lực rồi lưu lại cùng giá trị qua API; xác nhận Origin=DATABASE, không hạ ngưỡng để ép cảnh báo |
| SignalR thực | WebSocket client xác thực nhận `alert_created`, `alert_escalated`, `alert_updated`, `incident_created` |
| CRITICAL acknowledge | API thành công; request trùng nhận 409; đúng một Incident liên kết Warning và Camera |
| Cancel | Hủy cảnh báo thực từ camera thứ hai thành công; không tạo Incident cho cảnh báo đó |
| Tách LIVE/UPLOAD | Không có VideoAnalysisJob trong database acceptance; các alert quan sát được mang SourceType=LIVE |
| Stage/render snapshot | Có JPEG nguồn, boxes JSON và JPEG đã vẽ khung trong storage test; smoke test renderer/model đạt |
| Chặn Production | Unit tests xác nhận endpoint Simulator được chấp nhận ở Development/Testing, bị OptionsValidationException ở Production/Staging |
| Frontend | 59 tests đạt; build `--mode camera-test` đạt; ESLint các file frontend thay đổi đạt |
| Backend | 32 unit tests trong nhóm camera/risk/incident/ngưỡng đạt; 3 integration tests gồm acceptance thực và 2 camera tests đạt |
| AI smoke | 2 tests với model/tracker/renderer thực đạt |
| Script/source | Parse PowerShell và `git diff --check` đạt |

Lượt acceptance hai camera cuối quan sát 147 frame AI trong 25 giây polling, 1 track trên camera chính. Inference trung bình khoảng **27,5 ms**, tuổi frame lớn nhất khi polling khoảng **241 ms**. Đây không phải số đo latency video end-to-end; 5,88 frame/giây là số frame khác nhau bắt được qua polling, không kết luận đó là inference FPS thật. Chưa có số đo dropped frames/queue length. Báo cáo JSON chi tiết được giữ cục bộ ở `.runtime/live-acceptance.json`, không track vào git.

Trong 25 giây đo ở lượt này chỉ quan sát một AnalysisSession của camera chính. Reset/reconnect được kiểm chứng riêng bằng integration test; không tuyên bố đã quan sát risk engine xuyên qua mọi loại loop/network fault. Hai clip chưa có ground truth normal/drowning nên không dùng kết quả này để khẳng định độ chính xác nhận diện hay kịch bản Normal không báo động.

## Chưa kiểm chứng đầy đủ

- **Cloudinary upload thành công và ảnh từ CDN trong popup:** chưa cung cấp tài khoản Cloudinary test riêng. Acceptance dùng cấu hình test không upload thành công; snapshot tại thời điểm báo cáo đang PENDING. Stage/render đã chạy nhưng không coi là upload thành công.
- **Popup trong trình duyệt và giọng đọc thực tế:** SignalR server/client và logic lọc TTS đã được kiểm tra; chưa thao tác browser để xác minh hiển thị/đọc giọng, autoplay permission hoặc dedupe khi nhiều tab. Simulator không phát loa công cộng.
- **WARNING acknowledge khi vẫn còn WARNING:** không chạy nhánh đó trong acceptance cuối vì các cảnh báo đã nâng lên CRITICAL; unit tests nghiệp vụ liên quan vẫn đạt.
- **Các lỗi vận hành mở rộng:** chưa gây network interruption, crash cưỡng bức FFmpeg, restart MediaMTX khi API đang chạy, hoặc kiểm tra `loop:false` EOF end-to-end. Đã có cách tái hiện trong README; không đánh đồng Stop/Start một stream với toàn bộ các phép thử này.
- **Docker/LAN/TURN và Linux:** chưa chạy thực tế. Cấu hình mặc định chỉ mở loopback Windows; cần bind, allowlist/IP pinning và firewall riêng cho môi trường đó.
- **Quyền theo khu vực:** hệ thống hiện dùng nhóm ADMIN/LIFEGUARD cho toàn bộ LIVE alerts; chưa có mô hình quyền từng camera/khu vực. Giữ nguyên policy, dùng instance/DB test riêng để cô lập người nhận.

## File bổ sung/chỉnh sửa

Các file mới trong `tools/camera-simulator/`: `cameras.json`, `mediamtx.yml`, `common.ps1`, `publish-stream.ps1`, `start-simulator.ps1`, `stop-simulator.ps1`, `check-streams.ps1`, `videos/.gitkeep`, `.gitignore`, `README.md`, `VERIFICATION.md`.

Backend chỉnh các điểm tích hợp có điều kiện simulator: `CameraEndpoint`, `CameraTarget`, `CameraAddressPolicy`, `CameraWorker`, `CameraConnectionStatus`, validation trong `DependencyInjection`, `CameraFrameSource`, `LiveMonitoringWorker`; Python worker thêm tùy chọn reset tracker theo key khi phiên simulator đổi. Không đổi weights hoặc thuật toán/ngưỡng risk.

Frontend chỉnh `useSafetyTts`, `GlobalSafetyAlertsHost`, `GlobalAlertPopup`, `monitoringService`; thêm `criticalAnnouncement.js` và test. TTS chuyển sang sự kiện Incident sau xác nhận, bổ sung mode test im lặng và thời điểm trên popup.

Test bổ sung trong `CameraEquipmentTests`, `CameraContinuousAnalysisTests`, helper `SafetyMonitoringApiTests` và file acceptance opt-in `SimulatorLivePipelineTests.cs`. Root `.gitignore` chỉ bổ sung ngoại lệ để hai tài liệu simulator được track. Các file/video đang untracked trước tác vụ không được thêm hay sửa.

Không commit, seed camera vận hành hoặc khởi động simulator trong Production. Binary, video, cấu hình local, storage và database test đều nằm trong vùng ignored.

Sau kiểm thử đã chạy Stop simulator và tắt PostgreSQL test riêng; không để chúng tiếp tục chạy nền.

## Cập nhật: sửa box chồng và lệch đối tượng

- Phát hiện pool3 dùng video kết quả đã có box trong pixel. Đã so sánh frame với bản gốc, đổi cấu hình pool3 về MP4 gốc và bật lại riêng nguồn này; FFprobe xác nhận 1280×720, 25 FPS. Script chặn việc dùng lại file kết quả SmartPool khi bản gốc còn cùng thư mục.
- Camera AI dùng nguồn WebRTC đã vẽ khung trên đúng frame (`CameraAnalysisVideoSource`), tách khỏi nguồn preview gốc theo camera/mode; frontend không chồng thêm canvas metadata lên stream AI. Không tạo worker YOLO hoặc kết nối RTSP mới cho mode AI.
- Kalman tiếp tục nối danh tính, nhưng tọa độ output lấy measurement hiện tại; bỏ nội suy hiển thị 80 ms. Replay detection của video lỗi vẫn giữ ID chính xuyên 2.123 frame.
- Đã đạt: 8 tests tracking, 2 AI smoke tests, 28 unit tests camera/risk, 4 integration tests camera/WebRTC, 59 frontend tests, build và ESLint. Test WebRTC dùng pixel màu khác nhau để chứng minh preview AI nhận đúng frame phân tích, nguồn gốc vẫn độc lập và không mở thêm RTSP connection.
- Chưa đo FPS/mức trễ toàn video trên trình duyệt của người dùng. FPS AI preview phụ thuộc inference/encode. Cần khởi động lại backend và tải lại trang để nạp worker/API mới; timeline và video kết quả cũ không được sửa hồi tố.

## Cập nhật: ảnh trước khi lưu cảnh báo và hai cách xem chi tiết

- Popup LIVE mở camera tương ứng và làm nổi bật video, kể cả điều hướng từ cùng trang danh sách. Chi tiết tại Cảnh báo camera mở modal ảnh chụp thời điểm phát hiện.
- LIVE/UPLOAD stage frame vào outbox trên đĩa; render và upload thành công mới insert/update Warning với ImageUrl và SnapshotStatus=COMPLETED. Upload lỗi giữ tác vụ để retry, không chèn cảnh báo thiếu ảnh. Những bản ghi cũ thiếu ảnh vẫn giữ nguyên và có cơ chế sửa ảnh cũ.
- Đạt 11 integration tests: từ chối ảnh trống/HTTP trước insert, chuẩn bị snapshot không ghi DB, nâng mức dùng đúng ảnh, dedupe/xác nhận/cooldown/quyền UPLOAD, worker lỗi giữ DB rỗng và tiếp tục lưu khi có kết quả upload. Test phục hồi dùng URL fixture, không thay thế kiểm thử Cloudinary thực tế.
- Đạt 9 unit tests queue/risk và 59 frontend tests; frontend build và ESLint các component đã sửa thành công. Backend được build qua các project test, còn các warning có sẵn.
- Chưa chạy kiểm thử Cloudinary thực tế hoặc thao tác trình duyệt cho thay đổi này. Cần khởi động lại backend và tải lại frontend để áp dụng.

## Cập nhật: giảm giật khi chạy nhiều camera AI

- LiveMonitoringWorker giám sát các tiến trình AI độc lập theo camera, giữ tracker/risk session của camera còn lại khi bỏ một camera. Dùng thêm RAM/VRAM cho mỗi model resident, giới hạn MaxLiveCameras mặc định 4 (tối đa 8). Không mở thêm RTSP hoặc model riêng cho từng người xem.
- CameraAnalysisVideoSource dùng P-frame xen kẽ keyframe GOP 12, CRF 18, VBV 4 Mbps/1 Mb, không B-frame; nhận dạng đúng keyframe khi phát WebRTC và giới hạn pending encoder 4 frame. Cache font nhãn.
- Benchmark protocol với ba video cấu hình local (infer + render box, không đo trình duyệt/RTSP): tuần tự median 191,2 ms/vòng, khoảng 5,2 FPS/camera; ba worker đồng thời median 99,3 ms/vòng, khoảng 10,1 FPS/camera. Đây là benchmark phần xử lý, không cam kết FPS đầu cuối.
- Đạt 7 integration tests native: paired WebRTC giữ đúng pixel AI và có P-frame, hai camera ONNX tiếp tục phát độc lập sau khi xóa một camera, decoder liên tục/độ phân giải/reconnect. Đạt thêm 8 tracking tests và 2 smoke tests model/render/video. Backend build thành công, 12 warning có sẵn.
- Chưa đo độ mượt trên trình duyệt người dùng. Khởi động lại backend và tải lại trang để dùng code mới; simulator không cần đổi FPS hoặc restart.

## Cập nhật: kiểm thử nhiều popup đồng thời

- Chạy `node tools/camera-simulator/test-alert-popups.mjs` trên Chrome headless, viewport 1440×900, 320×740 và 844×390. Đạt 27 kiểm tra trình duyệt (9 trường hợp mỗi viewport); 59 frontend tests cũng đạt. Không thay đổi mã giao diện trong lượt kiểm tra này.
- Mỗi viewport nhận 10 cảnh báo LIVE qua SignalR giả lập, có cả WARNING và CRITICAL, mỗi cảnh báo có ảnh. Kiểm tra thứ tự mới nhất trước, event trùng/nâng mức không tạo popup trùng, cuộn đúng trong viewport, hai xác nhận đồng thời không khóa nhau, đóng/hủy/xem chi tiết đúng ID, lỗi xác nhận giữ popup để retry, tự đóng sau 30 giây, thời hạn riêng cho cảnh báo đến sau và không hiện lại qua polling.
- Phát video fixture thật với hai popup WARNING/CRITICAL cùng tồn tại, kiểm tra thứ tự, đóng riêng và timeout. Ảnh và báo cáo: `backend/.test-runtime/popup-burst/results.json` cùng sáu ảnh `ten-live-*.png`, `two-video-*.png`.
- API/SignalR dùng fixture, không ghi vào database thật. IncidentId trong response mock chỉ phục vụ kiểm tra UI; không dùng làm bằng chứng tạo Incident thật. Phần đó có integration API/database riêng.
- Script responsive cũ báo lỗi thời hạn video do đồng hồ vẫn chạy trong lúc thao tác/chụp ảnh. Bài test mới điều khiển đồng hồ và gửi heartbeat SignalR trong lúc tua thời gian; các kiểm tra 30 giây đã đạt trên cả ba viewport. Các lần thử lỗi của harness không được tính là lỗi sản phẩm.

## Cập nhật 12/11/2026: đo trực tiếp ba camera trên Chrome

- Paired AI trước thay đổi đạt 8,3–9,1 FPS/camera trong 15 giây. H.264 passthrough với overlay đồng bộ RTP đạt 24,6–25,2 FPS trong 15 giây: không rớt frame, không lỗi JavaScript/long task trên 50 ms. Có RTP timestamp trên cả ba video và canvas thực sự vẽ box.
- Đo tiếp 45 giây để đi qua lượt replay: 24,4 / 24,7 / 24,6 FPS; FPS trình bày 24,3 / 24,4 / 24,4. Khoảng cách frame p95 66,5 / 66,5 / 58,3 ms; lớn nhất 983,2 / 591,6 / 491,6 ms. Rớt 4 / 3 / 0 frame, không mất gói RTP, không lỗi JavaScript hoặc long task trên 50 ms. Khoảng ngắt ở replay vẫn tồn tại vì supervisor mở publisher mới. Đây là số đo Chrome headless tại máy local, không cam kết mạng triển khai khác đạt cùng kết quả.
- Video đi theo FPS nguồn, AI hoạt động độc lập. Metadata dùng cùng timestamp với RTP thực sự gửi tới browser; nội suy chỉ vị trí box, giữ severity/episode từ quan sát quá khứ. Mất dữ liệu/stale thì ẩn box. Trình duyệt thiếu timestamp hoặc nguồn không tương thích giữ paired fallback. Ảnh cảnh báo vẫn lấy frame phân tích thực, upload thành công mới ghi database.
- Binary JPEG protocol giảm encode/decode base64; decoder liên tục chia sẻ preview/health. Snapshot staging và SignalR chạy riêng để inference không đợi đĩa/client. Thêm khóa bảo vệ dictionary khi nhiều camera capture đồng thời, giới hạn bộ nhớ và gom các NAL cùng timestamp thành một frame.
- Đạt 14 unit tests mới/liên quan parser, access unit, RTP clock, bounded buffers, broadcaster và snapshot staging; 11 integration tests native camera/AI (bao gồm test timestamp smooth trùng timestamp frame capture và paired vẫn đúng pixel), bài Chrome ba camera qua cả 15 và 45 giây, 63 frontend tests, frontend build và ESLint các file sửa. Backend build thành công với warning có sẵn. Test tạo database GUID riêng, không tạo cảnh báo giả trong database vận hành.
- Kết quả local: `backend/.test-runtime/smooth-browser-15s.json`, `backend/.test-runtime/fluid-browser-results.json`, `backend/.test-runtime/fluid-three-cameras.png`. Backend mới đã chạy lại trên HTTPS 7019 / HTTP 5014, ba publisher giữ chạy theo yêu cầu. Cần tải lại frontend để nhận player mới.

Bài Chrome opt-in dùng script `tools/camera-simulator/test-live-playback.mjs`; integration test tự tạo API/database test và truyền token qua stdin. Ngoài biến native AI/FFmpeg và connection string test ở README, đặt:

```powershell
$env:SMARTPOOL_SIMULATOR_TEST_URL = 'rtsp://127.0.0.1:18554/pool1'
$env:SMARTPOOL_SIMULATOR_TEST_URLS = '["rtsp://127.0.0.1:18554/pool1","rtsp://127.0.0.1:18554/pool2","rtsp://127.0.0.1:18554/pool3"]'
$env:SMARTPOOL_LIVE_BROWSER_SCRIPT = (Resolve-Path tools/camera-simulator/test-live-playback.mjs).Path
$env:SMARTPOOL_LIVE_PERF_SECONDS = '45'
dotnet test backend/tests/SmartPool.IntegrationTests/SmartPool.IntegrationTests.csproj --filter 'FullyQualifiedName~SimulatorBrowserPlayback'
```

Cần Chrome và Playwright Core tại `backend/.test-runtime/ui-test/node_modules/playwright-core/index.mjs`. Khi đo, chỉ chạy một backend AI dùng ba stream để tránh benchmark bị ảnh hưởng bởi các model trùng. Bài test xác nhận tối thiểu 20 FPS mỗi video, có RTP metadata, không lỗi JavaScript, giữ frame khi waiting ngắn và ẩn khi waiting quá hạn. Jitter buffer đo được khoảng 222–295 ms; chưa đo latency end-to-end từ máy phát tới màn hình.

## Cập nhật: box nhấp nháy giữa các push metadata

- Nguyên nhân trong `CameraRiskFrameBuffer`: không có điểm tương lai thì ẩn sau 100 ms, trong khi broadcaster gửi tối đa mỗi 200 ms. Mỗi chu kỳ có thể xuất hiện nhiều video frame không box dù risk không thay đổi.
- Giữ box tối đa 600 ms theo RTP; dự đoán chuyển động tâm tối đa 200 ms từ hai quan sát cùng track, giữ kích thước thực và giữ severity/episode của quan sát quá khứ. Sau đó giữ vị trí đến giới hạn; không tiếp tục dự đoán vô hạn. Risk rỗng thật, stale một giây, offline và session mới vẫn xóa box.
- Đạt 66 frontend tests, bao gồm phát lại timeline 25 FPS với AI push 5 Hz có một push trễ: không frame mất box; kiểm tra giới hạn dự đoán, xóa risk thật, expiry, RTP wrap và session. Build frontend và ESLint thành công. Chưa đo lại ba luồng thật trong lượt sửa này; số FPS ở mục trước thuộc lần đo trước. Thay đổi chỉ ở frontend, tải lại trang để áp dụng.

## Cập nhật: làm mượt chuyển động và kích thước box

- Thêm `CameraBoxSmoother` sau bước lấy box theo RTP, dùng chung cho danh sách/phóng to thông qua `useCameraRisks`. Lọc tâm và kích thước mỗi video frame, không thêm hàng chờ phát video hoặc React render mỗi frame. Hằng số phản hồi cơ sở 80 ms cho vị trí, 110 ms cho kích thước; tự giảm khi chuyển động/thay đổi kích thước lớn để bám nhanh. Đổi lại, các hiệu chỉnh tọa độ nhỏ được áp dần thay vì nhảy tức thời.
- Màu/severity/episode lấy ngay từ risk đang hiển thị. Xóa ngay khi risk biến mất; reset bộ lọc khi đổi episode/session, RTP đi lùi hoặc gián đoạn trên 250 ms. Mỗi track có trạng thái riêng, không sửa tọa độ AI gốc hoặc ảnh snapshot.
- Đạt 70 frontend tests, build và ESLint. Test mới đo giảm biên độ rung vị trí/kích thước ở 25 FPS, bám chuyển động lớn không vượt mục tiêu, cập nhật severity ngay, reset và RTP wrap/biên ảnh. Chưa kiểm tra trực quan lại ba camera thật trong lượt này; cần tải lại frontend.

## Cập nhật: đo độ liền mạch box trên ba luồng thật

- Chạy Chrome với API/model/database test thật, ghi riêng camera_analysis và các lần canvas vẽ trên timestamp video; không ghi token/SDP hoặc tạo cảnh báo giả trong database vận hành. Khi bắt đầu, ba publisher đã dừng và backend IDE dùng output khác; xác minh đúng process rồi bật lại ba nguồn, tạm dừng backend trong phép đo và tự khởi động lại sau đó.
- Trước sửa (25 giây): push median khoảng 207 ms; tuổi quan sát trong metadata median 428 / 556 / 176 ms. Tỷ lệ video frame chưa có metadata tương lai để nội suy là 80,7% / 82,5% / 34,3%. Vì vậy chỉ thêm bộ lọc tọa độ vẫn phải dự đoán/giữ vị trí rồi sửa lại khi dữ liệu tới.
- Bật FFmpeg AVIO direct, giới hạn pipe read 4096 byte để giảm read-ahead trên nguồn bitrate thấp, vẫn giải mã tất cả frame để giữ ánh xạ timestamp. Giảm chu kỳ broadcaster xuống 75 ms; timeout client chậm vẫn giới hạn 200 ms, không chặn inference. Yêu cầu jitter buffer video 400 ms thay 300 ms để có điểm nội suy kế tiếp. Không đổi RiskDecisionEngine hoặc snapshot lưu database.
- Sau sửa (25 giây): push median 78–79 ms, p95 116–121 ms. Tuổi metadata median 183 / 183 / 168 ms, p95 282 / 252 / 250 ms. Tỷ lệ phải dự đoán giảm còn 3,2% / 2,1% / 4,0%. Đây là khả năng có dữ liệu để nội suy theo RTP, không phải số đo độ chính xác detection hoặc latency end-to-end.
- Video đạt 23,9 / 24,6 / 24,0 FPS, không lỗi JavaScript hoặc long task trên 50 ms. Qua điểm replay vẫn có ngắt nguồn ngắn; có 13 frame liên tiếp không box ở hai camera tại khoảng reset/thiếu quan sát, camera thứ ba không có khoảng mất box khi trace kỳ vọng risk. Không kết luận box luôn hiện khi model không trả risk hoặc camera mất quan sát.
- Đạt bài Chrome ba camera, 9 native integration tests giải mã/kích thước/concurrent disposal/paired pixels/RTP clock/reconnect, 6 unit tests parser/broadcaster, 70 frontend tests, frontend build và ESLint. Backend mới đã bật lại trên HTTPS 7019, giữ ba publisher chạy. Cần tải lại frontend để dùng buffer mới.
- Artifact local: `backend/.test-runtime/box-continuity-before.json`, `box-continuity-before-cameras.json`, `box-continuity-after.json`, `box-continuity-after-cameras.json`; báo cáo FPS/ảnh vẫn ở các đường dẫn fluid-browser. Đọc trace với `node tools/camera-simulator/analyze-box-continuity.mjs [trace.json] [camera-diagnostics.json]`. Tên camera trong analyzer tương ứng fixture Performance Camera 1–3.

## Cập nhật 13/11/2026: nhấp nháy khi confidence giảm thoáng qua

- `RiskDecisionEngine.Observe` bỏ qua detection không đủ confidence, trả danh sách quyết định rỗng cho frame đó nhưng vẫn giữ severity/episode trước đó. Frontend từng coi danh sách rỗng này như hết nguy cơ và xóa box ngay. Thêm `ActiveTrackIds()` chỉ đọc trạng thái engine và truyền `activeRiskTrackIds` qua metadata để phân biệt hai trường hợp.
- Canvas có thể giữ quyết định thực gần nhất tối đa 300 ms khi backend xác nhận track vẫn có nguy cơ, dùng box detection hiện tại của cùng track nếu có. TTL tính từ quyết định thực, không tự gia hạn bởi box giữ lại. Khôi phục được xác nhận, tracking session mới, offline hoặc quá hạn vẫn xóa. Đây là quy tắc hiển thị; AlertSnapshotQueue vẫn chỉ nhận các quyết định Observe thực, không persist dữ liệu giữ box.
- Đạt 74 frontend tests và 9 unit tests risk/state. Các test xác nhận confidence thấp/mất quan sát không giải quyết nguy cơ, phục hồi thật giải quyết được, metadata tách active IDs khỏi quyết định rỗng, nhiều track không trùng, TTL không tự gia hạn, offline và session reset. Build frontend/backend và ESLint thành công với warning build có sẵn.
- Chrome chạy 35 giây với 3 camera/model/API/database test thật. Replay trace so sánh có/không dùng active IDs tìm được 13 / 10 / 25 frame sẽ mất box ở cơ chế cũ; canvas thực tế vẫn vẽ trong cả 48 frame đó. Video 24,5 / 24,1 / 24,6 FPS, không lỗi JavaScript/long task trên 50 ms. Các khoảng không box khác do reset/mất quan sát hoặc model không có risk vẫn tồn tại; không suy ra mọi frame đều phải có box.
- Trace hiện tại `backend/.test-runtime/box-continuity-trace.json` ghi cả active IDs; `node tools/camera-simulator/analyze-box-continuity.mjs` xuất `bridgedFrames` và `drawnBridgedFrames`. Backend mới đã bật lại HTTPS 7019 / HTTP 5014, ba publisher giữ chạy. Cần tải lại frontend và dùng backend mới để có trường metadata này.

## Cập nhật 13/11/2026: giảm trễ popup và khôi phục AI vận hành

- LIVE đã có JPEG được worker AI vẽ box. Outbox hiện lưu kèm JPEG này và dùng ngay khi upload, tránh mở Python/render lại cho từng cảnh báo; nguồn và box gốc vẫn lưu cho phục hồi. UPLOAD hoặc job cũ chưa có JPEG render tiếp tục dùng renderer dự phòng.
- Ba consumer outbox độc lập, mỗi tác vụ có lease/scope/timeout riêng. Các tác vụ cùng source/track/episode được khóa trong process để tránh upload trùng đồng thời, ưu tiên LIVE CRITICAL rồi LIVE WARNING trước UPLOAD. Stage xong đánh thức consumer, fallback kiểm tra 200 ms; tác vụ retry/backoff không chặn camera khác. Tiếp tục upload thành công mới RecordAsync/SignalR, không ghi cảnh báo thiếu ảnh.
- Thêm ObservedAt UTC vào AlertDto. Popup xét tuổi quan sát (cửa sổ hai phút hiện có), không lấy thời điểm ghi DB làm tuổi sự kiện. Cảnh báo cũ upload xong muộn vẫn lưu lịch sử nhưng không xuất hiện như cảnh báo LIVE mới; nâng mức với quan sát mới vẫn được nhận. Popup và bảng hiển thị thời điểm phát hiện.
- Phát hiện bản backend từ artifacts dùng keyring mặc định khác output IDE, khiến ba cấu hình camera không giải mã được dù RTSP đang phát. Đã trỏ runtime và `.env` local về keyring IDE cũ, không thay khóa/metadata camera. Đây là lỗi runtime của lần khởi chạy artifacts trước; các phép đo camera API test riêng trước đó không chứng minh được camera trong app vận hành đã restore.
- Sau khởi động lại đúng keyring, log xác nhận ba Live AI worker ready và cảnh báo LIVE thật lưu với ảnh. Hai mẫu đầu có processing 2913 / 939 ms, tuổi quan sát lúc lưu/phát sự kiện 3096 / 1103 ms. Đây là latency backend gồm ảnh/upload/RecordAsync, chưa đo thời điểm popup được vẽ trên màn hình. Không cam kết popup tức thì khi Cloudinary/mạng chậm, và không hạ ngưỡng hold nguy cơ.
- Sáu mẫu LIVE tiếp theo có processing 731 / 846 / 646 / 821 / 690 / 860 ms; tuổi quan sát lúc lưu/phát sự kiện 891 / 959 / 780 / 995 / 827 / 1024 ms. Những số này bắt đầu ở frame được đánh giá, không cộng thời gian tích lũy cửa sổ/giữ ngưỡng Khẩn cấp.
- Đạt 5 unit tests queue/concurrency: giữ pixel render, phục hồi ưu tiên CRITICAL, slow episode không chặn hai camera khác, không xử lý trùng episode đồng thời. Đạt 10 integration tests image-first/ACK Incident/cancel, 75 frontend tests, 27 kiểm tra popup trình duyệt ở ba viewport, frontend/backend build và ESLint. Test upload phục hồi dùng URL fixture, đo vận hành ở trên dùng các quyết định thật của camera demo; không chèn cảnh báo giả vào database vận hành.

## Cập nhật 13/11/2026: xác nhận popup Chú ý đã bị nâng mức

- Tái hiện lỗi đua trạng thái: UI còn hiển thị WARNING, backend đã nâng cùng Warning ID lên CRITICAL. Request cũ chỉ chứa ID/action, nên ACK dựa trên mức DB hiện tại và tạo Incident dù người dùng vừa nhìn thấy Chú ý.
- API acknowledge yêu cầu body `{ "expectedSeverity": "WARNING" }` (hoặc CRITICAL/EMERGENCY đang hiển thị). Repository so sánh mức sau khi khóa bản ghi; khác mức trả 409 kèm Alert hiện tại, không đổi trạng thái hoặc tạo Incident. Client cũ không gửi mức bị từ chối 400 để yêu cầu tải lại. Internal repository callers còn giữ tham số tùy chọn để tương thích các luồng nội bộ.
- Global popup và bảng gửi severity của chính bản hiển thị khi click. Khi nhận 409 có Alert mới, popup cập nhật ngay và mở lại nút, không tự gửi ACK lần hai. Chỉ sau thao tác xác nhận tiếp theo trên Khẩn cấp mới tạo Incident. WARNING khớp mức vẫn chỉ ACK, không tạo Incident.
- ACK/CANCEL cùng dùng advisory lock theo episode trước row lock, giống thứ tự RecordAsync. Vì vậy ACK WARNING đồng thời với escalation hoặc bị từ chối do mức mới, hoặc hoàn tất WARNING rồi escalation tạo cảnh báo CRITICAL mới còn PENDING. Không âm thầm đổi cảnh báo CRITICAL thành đã xử lý, không tạo Incident từ ACK WARNING.
- Đạt 7 integration tests, gồm tái hiện popup cũ, request thiếu mức, xác nhận đúng mức, ACK/esc concurrent, WARNING không Incident, CRITICAL có đúng một Incident và critical follow-up. Đạt 75 frontend tests, 30 kiểm tra popup Chrome ở ba viewport (có trường hợp chặn ACK Chú ý cũ và yêu cầu xác nhận Khẩn cấp lần hai), build và ESLint. Kiểm tra UI dùng API fixture; kiểm tra database dùng PostgreSQL test GUID riêng.
- Kiểm tra thêm upload CRITICAL đã chọn ID của WARNING trước khi ACK WARNING hoàn tất: RecordAsync không tái sử dụng ID đã xử lý, PrepareSnapshotAsync cũng cấp ID mới khi cần. Test critical follow-up tái hiện chính upload target cũ này. Asset Cloudinary dùng ID riêng của outbox item để ảnh CRITICAL đến sau không ghi đè ảnh của WARNING đã xử lý.

## Cập nhật 13/11/2026: box chậm hơn người trong hình

- Bộ lọc `CameraBoxSmoother` áp dần vị trí/kích thước trong 80/110 ms nên thêm độ trễ sau bước đồng bộ RTP. Đã bỏ bộ lọc cùng trạng thái frame trước khỏi hook, thay bằng nội suy cubic đơn điệu trên timeline RTP khi có các quan sát lân cận. Chỉ có hai điểm dùng tuyến tính; mọi endpoint giữ đúng tọa độ AI, không overshoot tâm/kích thước ngoài khoảng quan sát, giới hạn trong vùng ảnh. Severity/episode và giới hạn giữ box không đổi.
- Đạt 76 frontend tests, gồm chuyển động đều tại các thời điểm lấy mẫu không đều, endpoint chính xác, vận tốc liên tục qua điểm AI, đổi hướng không vượt vị trí quan sát, RTP wrap và biên ảnh. Frontend build/ESLint đạt. Ba integration tests ACK cũ/ACK-esc đồng thời/ảnh CRITICAL đang upload đã chạy lại thành công sau lần private PostgreSQL không hoạt động; các lần lỗi kết nối trước đó không được tính là lỗi sản phẩm.
- Chrome với 3 stream/model/API thật trong DB test riêng, 25 giây: 24,3 / 24,6 / 24,9 FPS, không lỗi JavaScript hoặc long task trên 50 ms. Khoảng 99% frame có metadata tương lai để nội suy. Tại 146 / 150 mẫu có risk của camera 1/2, so sánh canvas với box AI gốc ở cùng timestamp: p95 sai lệch tâm/kích thước bằng 0 pixel. Camera 3 không có risk trong đoạn này nên không có mẫu đánh giá box; không tạo cảnh báo giả để lấp số liệu.
- Đây là độ chính xác hiển thị so với output AI, không phải đánh giá model với nhãn thủ công. Các khoảng thiếu quan sát/reset session vẫn giữ nguyên quy tắc ẩn. Trace lưu cả timestamp mà canvas đã vẽ, kích thước vùng ảnh và video để tính đúng letterbox. Analyzer xuất số mẫu và sai lệch pixel. Artifact `backend/.test-runtime/box-continuity-trace.json`, `fluid-browser-results.json`, `fluid-three-cameras.png`.
- Backend mới trong `severity-ack-final-build` đã khởi động lại trên HTTPS 7019 / HTTP 5014, giữ cấu hình keyring cũ. Bản này gồm cả guard expectedSeverity và xử lý snapshot target cũ; cần tải lại frontend để lấy cách nội suy mới.
