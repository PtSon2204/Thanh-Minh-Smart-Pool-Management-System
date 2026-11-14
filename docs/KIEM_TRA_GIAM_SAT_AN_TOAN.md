# Báo cáo kiểm tra luồng giám sát an toàn

Ngày kiểm tra và sửa: **13/11/2026**, đối chiếu working tree hiện tại. Phần cập nhật dưới đây là trạng thái sau sửa; các mục audit phía sau giữ lại tình huống và bằng chứng trước sửa để đối chiếu.

## Cập nhật sau sửa

**Thời hạn giao diện hiện tại:** popup hiện 10 giây. Cảnh báo camera LIVE còn PENDING chỉ có Xác nhận/Hủy trong 10 giây từ ObservedAt (fallback CreatedAt); sau đó chỉ còn Xem chi tiết ở danh sách và xem trong dialog. Hết thời gian chỉ đổi thao tác giao diện, không tự ACK/CANCEL hoặc tạo Incident. Sau thay đổi, 42 test frontend và 36 kiểm tra browser đạt, gồm ranh giới 9.999/10.000 ms, nâng mức mới, cảnh báo đã cũ, dialog đang mở và popup video 10 giây. Build/lint đạt.

**Cập nhật giao diện chi tiết LIVE:** nút Xem chi tiết trên popup hiện mở LiveAlertDetailsModal ngay tại trang đang xem, phát camera cùng thông tin cảnh báo, cho xác nhận/hủy trong cửa sổ thao tác và xem ảnh lúc phát hiện. Cửa sổ camera không phụ thuộc thời hạn 10 giây của notification; khi đóng sẽ giải phóng view WebRTC. Thao tác ở trang Cảnh báo camera vẫn xem ảnh snapshot. Lượt kiểm tra lúc triển khai dialog có 36 test frontend và 33 kiểm tra browser đạt, gồm phát video WebRTC bằng nguồn canvas fixture, giữ dialog sau timeout và xử lý đúng Alert ID; không dùng camera hoặc database vận hành để tạo cảnh báo test.

**Bổ sung sửa mất box khi mở chi tiết:** LiveRiskOverlay giữ màu nguy cơ và geometry cho cả video paired; metadata DisplayRisks giúp viewer mới khởi tạo box khi confidence thấp và không có RiskDecision mới. Hook nạp timeline riêng từ shared query cache cho mọi observer. DisplayRisks không được lưu thành bằng chứng cảnh báo; nguồn lệnh ghi DB vẫn là Risks thật. Bài test mới đã tái hiện trước sửa 0 box thay vì 1; sau sửa 680 unit backend, 38 test frontend và 36 kiểm tra browser đạt. Nguồn video browser là nền trơn, box do canvas ứng dụng tự vẽ từ metadata có risks=[]/displayRisks khác rỗng, không vẽ sẵn box vào fixture. Backend đã được nạp bản mới trên localhost:7019/5014, giữ nguyên tiến trình publisher camera.

**F01–F08 đã được xử lý trong code.**

| Phần | Thay đổi |
|---|---|
| F01 | Dismiss và timer theo Alert ID + severity; nâng mức có đủ 10 giây mới; event/poll cùng mức đã đóng không mở lại popup |
| F02 | Tách RAM LIVE CRITICAL và phần thường, mỗi hàng tối đa 16 mục; thêm dự phòng 16 mục CRITICAL trên đĩa; giữ/retry mục đã nhận nếu staging chưa thành công; đếm/log từ chối khi RAM đầy |
| F03 | Cache risk đã chốt theo track, cập nhật geometry bằng detection thật dù confidence chưa đủ tạo decision; không phụ thuộc lịch sử 32 mẫu; mất detection thật vẫn hết hạn sau 300 ms |
| F04 | Tuần tự hóa stop/start SignalR, đóng transport khi logout, xác thực lại khi token/tài khoản đổi; chặn ReceiveNotification từ phiên cũ trong lúc chuyển danh tính |
| F05 | Gửi metadata song song tối đa 8 camera, scope và timeout riêng |
| F06 | Camera Connected ưu tiên slot; giữ ổn định worker đang chạy; camera mất kết nối chỉ giữ slot theo LiveReconnectGraceSeconds, mặc định 15 giây |
| F07 | HTTP CANCEL cũng yêu cầu expectedSeverity; dữ liệu màn hình cũ trả 409 kèm Alert mới và chưa hủy Khẩn cấp |
| F08 | CopyFrom/Clone cùng khóa; mỗi Observe dùng một snapshot ngưỡng nhất quán, không reset episode |

Đã xóa PairedCameraPlayer.jsx không còn caller, bỏ 5 dictionary và các vòng lặp dư trong worker một camera, bỏ SnapshotRequired luôn false khỏi hợp đồng và các caller/test. Project audit tạm đã được bỏ; test hồi quy đã chuyển vào bộ test chính. RiskOptions.Clone hiện được dùng thật để lấy snapshot nhất quán. Luồng phục hồi ảnh legacy vẫn được giữ vì còn caller.

Đã đổi thứ tự hosted service để khi shutdown: producer LIVE/video dừng trước, staging drain, rồi uploader mới dừng. Hàng đợi RAM vẫn hữu hạn; TryStage=false là chưa được nhận, có counter/log. RAM không phải lưu trữ chống crash: kill tiến trình hoặc đĩa hỏng kéo dài quá thời hạn shutdown vẫn có thể làm mất mục chưa stage bền vững. Chỉ file outbox đã ghi mới được bảo đảm phục hồi sau restart.

Kết quả **sau sửa**:

| Kiểm tra | Kết quả |
|---|---|
| Backend unit, gồm các test hồi quy mới | **677 đạt**, không bỏ qua |
| Frontend safety, gồm test audit và geometry kéo dài | **36 đạt**, không bỏ qua |
| Integration/API | **30 đạt**, 6 ca có điều kiện skip ở lượt đầu |
| Native decoder/WebRTC/ONNX/video upload | **10 đạt**, gồm 2 ca bị skip ở lượt đầu |
| Popup trong browser, 3 viewport | **30 kiểm tra đạt**, API/SignalR fixture |
| Frontend production build và lint các file ứng dụng frontend đã sửa | Đạt |

Còn 4 ca có điều kiện ngoài fixture chưa chạy lại: Cloudinary thật và ba bài simulator/nhiều camera/browser LIVE. Không suy ra FPS thực tế của 3 camera demo từ các test này. PostgreSQL test đã được dừng sau kiểm thử; database ứng dụng không dùng để kiểm thử ghi dữ liệu.

### Kiểm tra tiếp F09 và F10

**F09 đã tái hiện thêm một lỗi thực và sửa:** fixture H.264 baseline mã hóa ID 0–15 ngay trong mức sáng của pixel, RTP tăng theo từng ID. Trước sửa, ca video bình thường đạt nhưng ca cố tình hỏng một picture thất bại: JPEG mang ID 10 bị gắn RTP của ID 9. Decoder đã bỏ một picture trong khi hàng đợi timestamp vẫn dequeue tuần tự.

Sau sửa, CameraAnalysisFramePump dùng -xerror/-err_detect explode, theo dõi stderr ở mức error, hủy pump và xóa Latest ngay khi giải mã lỗi. CameraFrameSource nhận biết pump đã bị hủy ngay, tạo lại từ keyframe. Không tiếp tục dùng một hàng timestamp đã mất đồng bộ. Không ghi nội dung chẩn đoán FFmpeg hoặc dữ liệu camera vào log.

[CameraPixelAlignmentTests.cs](../backend/tests/SmartPool.IntegrationTests/ManageEquipments/CameraPixelAlignmentTests.cs) đối chiếu **pixel thật** với RTP: 2/2 ca đạt sau sửa. Lượt kiểm tra thêm có 9 ca decoder/pixel đạt; paired WebRTC và giữ phiên xem khi reconnect được chạy riêng, 2/2 đạt. Tổng các integration/native riêng biệt đã đạt qua các lượt sau sửa là 43; không cộng lại các ca đã chạy lặp.

Giới hạn của bằng chứng: fixture hiện là H.264 baseline, picture độc lập, có lỗi bitstream chủ động. Chưa chứng minh mọi nguồn H.265/B-frame/reorder hoặc mọi kiểu hỏng gói; đường giải mã vẫn dùng FIFO và chưa truyền PTS xuyên container. Đây không phải kiểm định độ chính xác nhận người của model.

**F10 đã có đo riêng:** metadata camera bổ sung RenderMilliseconds và AnalysisMilliseconds bên cạnh InferenceMilliseconds. AnalysisMilliseconds bao gồm capture/AI/risk/render đến trước công bố state, không bao gồm upload hoặc delay giữa các vòng; không dùng nghịch đảo giá trị này làm FPS toàn hệ thống.

[benchmark-ai-render.py](../tools/camera-simulator/benchmark-ai-render.py) chạy ONNX và giao thức binary thật, CPU 1 thread, ảnh mẫu 540×720, 10 vòng đo sau warm-up, box chỉ dùng để đo render, không ghi DB:

| Box mẫu | Inference median | Render median | Infer + render median |
|---|---|---|---|
| 1 | 131,85 ms | 5,37 ms | 136,59 ms |
| 5 | 115,05 ms | 7,56 ms | 123,15 ms |

Chênh lệch inference giữa hai lượt là biến thiên đo/warm-up/tải máy, không phải do thêm box làm model nhanh hơn. Trong fixture này render không chiếm phần lớn thời gian; giữ render đúng frame cho paired video/snapshot. Font đã có cache từ trước. Chưa thay đường phát simulator ở F11; hết lượt vẫn có reconnect và reset session.

Artifact bổ sung: safety-pixel-before.trx (1 lỗi tái hiện), safety-pixel-after.trx, safety-paired-final.trx và render-timing.json trong backend/.test-runtime/safety-audit/. Unit backend chạy lại sau thay đổi vẫn 677/677 đạt.

Artifact sau sửa: backend/.test-runtime/safety-audit/safety-fixed-unit.trx, safety-fixed-integration.trx, safety-fixed-native.trx và báo cáo popup tại backend/.test-runtime/popup-burst/. Phần mô tả luồng hiện hành đã cập nhật trong [LUONG_GIAM_SAT_AN_TOAN.md](LUONG_GIAM_SAT_AN_TOAN.md).

## 1. Kết luận audit trước sửa

Các test hiện có đạt không đồng nghĩa luồng đã bao phủ mọi tình huống. Bộ audit bổ sung đã tái hiện các điểm sau:

| Mã | Ưu tiên | Phát hiện | Bằng chứng |
|---|---|---|---|
| F01 | P1 | Đóng/hết hạn popup Chú ý có thể chặn luôn popup Khẩn cấp khi nâng mức cùng Warning ID | Test frontend thất bại |
| F02 | P1 | Cảnh báo Khẩn cấp được nhận vào RAM vẫn có thể bị bỏ khi outbox đĩa đầy | Test C# thất bại |
| F04 | P1 | Đổi tài khoản/token không tái xác thực kết nối SignalR đang Connected | Test service với transport giả thất bại |
| F03 | P2 | Box mất sau 300 ms thiếu RiskDecision dù track vẫn được quan sát và engine vẫn đánh dấu nguy cơ | Test frontend thất bại; phản ánh giới hạn thiết kế hiện tại |
| F05 | P2 | Metadata của camera khỏe phải chờ các camera gửi chậm trước nó | Test C# đo khoảng 1,5 giây ở kịch bản 8 camera |

P1: nên xử lý trước khi demo/vận hành nhiều người, nhiều cảnh báo. P2: ảnh hưởng độ mượt, khả năng quan sát hoặc tính nhất quán trong các điều kiện cụ thể.

Ở lượt audit trước sửa, các test bổ sung là **đặc tả hành vi mong muốn** và đã đỏ để ghi nhận vấn đề. Sau sửa chúng đã đạt và được đưa vào [bộ test unit backend](../backend/tests/SmartPool.UnitTests/Features/SafetyMonitoring/) và [safetyAudit.test.mjs](../frontend/thanh-minh-smart-pool/src/features/safety/utils/safetyAudit.test.mjs). Riêng F03 yêu cầu thay đổi chính sách giữ geometry, không được diễn giải là test cũ bị hỏng.

## 2. Phạm vi rà soát

Đã đọc đường đi chính và các nhánh lỗi của:

1. Program/DI, CameraSupervisor, CameraService và registry camera.
2. CameraWorker, RTP frame buffer/clock, giải mã JPEG liên tục và vòng reconnect.
3. CameraFrameSource, LiveMonitoringWorker, AiWorkerClient, Python Detector/CameraTracker.
4. RiskDecisionEngine và cập nhật nóng ngưỡng.
5. CameraAnalysisState, broadcaster, SignalR, WebRTC, frontend lấy metadata và nội suy/vẽ box.
6. Hàng đợi RAM, disk outbox, render/upload snapshot, MonitoringRepository.
7. Popup, timeout/dismiss, xem chi tiết, ACK/CANCEL, Incident và âm báo/TTS.
8. Upload video, kiểm tra timeline, inference, render, encode, phát lại và quyền truy cập.

Tài liệu giải thích chuỗi hàm: [LUONG_GIAM_SAT_AN_TOAN.md](LUONG_GIAM_SAT_AN_TOAN.md).

Không coi việc đọc code và chạy unit test là kiểm định độ chính xác cứu hộ của model. Không dùng dữ liệu ứng dụng để chạy các test ghi database. Các integration test dùng PostgreSQL test riêng trên localhost:15433 và database tạm do fixture tạo/xóa.

## 3. Kết quả kiểm thử ở lượt audit trước sửa

| Bộ kiểm thử | Kết quả |
|---|---|
| Toàn bộ SmartPool.UnitTests | 671 đạt, 0 lỗi, 0 bỏ qua |
| Trong bộ trên, tên test thuộc SafetyMonitoring/camera | 73 đạt |
| Frontend src/features/safety | 31 đạt, 0 lỗi |
| Python test_tracking.py | 8 đạt, 0 lỗi |
| Integration SafetyMonitoringTests lần đầu | 29 đạt, 0 lỗi, 6 bỏ qua vì điều kiện native/dịch vụ ngoài chưa được bật |
| Integration native bổ sung | 10 đạt, 0 lỗi, 0 bỏ qua; gồm 2 ca native bị skip ở lượt đầu |
| Audit frontend bổ sung | 3 thất bại, tương ứng F01/F03/F04 |
| Audit C# bổ sung | 2 thất bại, tương ứng F02/F05 |

Lượt native bổ sung kiểm tra decoder RTSP dùng chung, capture nhiều reader/disposal, kích thước/tỉ lệ ảnh, cập nhật health/preview, session simulator, paired WebRTC, binary transport với ONNX thật và 2 job video upload/render/playback. PostgreSQL test đã được dừng sau khi hoàn tất.

Còn **4 ca có điều kiện chưa chạy**: Cloudinary thật, luồng LIVE với nguồn simulator ngoài fixture, nhiều camera LIVE với model thật khi gỡ một camera, và kiểm thử trình duyệt với 3 camera thật. Vì vậy không kết luận đã đo lại đầy đủ FPS/độ mượt E2E của ba camera demo hoặc đã kiểm định upload Cloudinary thật trong lượt này. Hai ca BinaryAiTransport và VideoUpload native bị skip ở lượt đầu đã chạy đạt trong lượt bổ sung.

Các phần được test hiện có kiểm tra gồm: điều kiện WARNING/CRITICAL/recovery/cooldown, độc lập tracker/episode, dữ liệu AI stale, quyền SignalR, RTP wrap, tính liên tục của box theo mẫu AI, hàng đợi ảnh hữu hạn, chống trùng/nâng mức Warning, ACK và Incident, cạnh tranh ACK với nâng mức, yêu cầu ảnh trước khi ghi Warning, quyền sở hữu video và lưu timeline.

Các file TRX của lượt kiểm tra nằm tại backend/.test-runtime/safety-audit/; chúng là artifact local bị gitignore:

- safety-audit-unit.trx
- safety-audit-integration.trx
- safety-audit-regressions.trx
- safety-audit-native.trx

## 4. F01 — Popup đã đóng có thể che mất lần nâng mức Khẩn cấp

**Vị trí:** [useGlobalSafetyAlerts.js](../frontend/thanh-minh-smart-pool/src/features/safety/hooks/useGlobalSafetyAlerts.js), dòng 22–36 và 70–74; [alertState.js](../frontend/thanh-minh-smart-pool/src/features/safety/utils/alertState.js), reconcilePendingAlerts; [MonitoringRepository.cs](../backend/src/SmartPool.Infrastructure/Persistence/Repositories/MonitoringRepository.cs), RecordAsync.

Chuỗi tái hiện:

~~~text
Warning A = WARNING, PENDING → popup xuất hiện
Người dùng Đóng / Xem chi tiết / popup tự hết 30 giây
  → dismissed.add(A.id)
AI tiếp tục theo dõi, A nâng lên CRITICAL
  → repository UPDATE cùng Warning ID
  → alert_escalated
Frontend nhận A nhưng dismissed.has(A.id) = true
  → không hiện lại popup; polling cũng loại A
~~~

Không cần người dùng ACK/CANCEL trong tình huống này. Đóng popup là thao tác local nhưng đang chặn cả một lần nâng mức nghiệp vụ sau đó. Khi không còn cảnh báo khác, còi Khẩn cấp cũng không được kích hoạt bởi cảnh báo đã bị loại này.

Ngoài ra, timer expiry được lưu theo ID và không làm mới khi nâng mức. Nếu WARNING còn 1 giây trước timeout rồi thành CRITICAL, popup mới chỉ còn khoảng 1 giây để hiển thị.

**Test:** AUDIT F01 kỳ vọng nhận được 1 CRITICAL sau lần dismiss WARNING; thực tế 0. Test gọi helper polling thật; nhánh SignalR sử dụng cùng điều kiện chặn theo ID qua kiểm tra code.

**Hướng xử lý:** lưu dismissal/expiry theo ID và phiên bản mức cảnh báo, cho phép nâng mức vượt dismissal của mức trước; tạo lại thời gian hiển thị khi WARNING → CRITICAL. Dismiss một CRITICAL rồi polling cùng bản CRITICAL không được làm nó xuất hiện liên tục.

## 5. F02 — Nhận vào RAM chưa bảo đảm sự kiện được ghi vào outbox

**Vị trí:** [AlertSnapshotQueue.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/AlertSnapshotQueue.cs), TryStage/StageAsync; [AlertSnapshotStagingWorker.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/AlertSnapshotStagingWorker.cs), ExecuteAsync dòng 23; [LiveMonitoringWorker.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/LiveMonitoringWorker.cs), dòng 149.

Chuỗi tái hiện có test:

~~~text
Outbox đã có 16 mục, MaxPendingAlertSnapshots=16
TryStage(CRITICAL mới) → true: mục đã vào RAM
StagingWorker lấy mục ra
StageAsync(CRITICAL) → false vì đĩa đã đủ số mục
StagingWorker bỏ qua kết quả false; không giữ lại mục để retry
Outbox cuối cùng chỉ còn 16 WARNING cũ, không có CRITICAL mới
~~~

Đây không chỉ là trạng thái báo đầy hợp lệ. Một mục producer đã được báo nhận vẫn bị mất ở bước sau. Đổi giá trị giới hạn thành 256 mặc định chỉ dời thời điểm xảy ra.

Ở tầng RAM cũng có vấn đề tương tự: channel có sức chứa 16 và TryStage có thể trả false; LiveMonitoringWorker bỏ qua kết quả này. Mức ưu tiên LIVE CRITICAL chỉ áp dụng khi đọc các file đã được stage, không bảo đảm CRITICAL lọt được qua hàng đợi đầy.

Nếu người đó vẫn được AI đánh dấu ở các giây tiếp theo thì có thể stage lại sau khi trống. Nếu đây là quan sát cuối trước khi mất track, đóng camera hoặc dừng tiến trình thì sự kiện đó có thể không bao giờ thành Warning/popup.

**Test:** F02 kỳ vọng episode critical-new có trong outbox sau khi staging worker drain; thực tế không có.

**Hướng xử lý:** định nghĩa rõ hợp đồng accepted/durable; giữ/retry mục staging chưa ghi được; có quota/ưu tiên cho chuyển mức CRITICAL; đo và hiển thị số mục bị từ chối. Không chặn inference vô hạn và không đổi sang ghi Warning thiếu ảnh.

## 6. F03 — Box phụ thuộc lần RiskDecision cuối thay vì lần thấy người cuối

**Vị trí:** [RiskDecisionEngine.cs](../backend/src/SmartPool.Application/Features/SafetyMonitoring/RiskDecisionEngine.cs), lọc confidence dòng 81–85; [cameraRiskFrameBuffer.js](../frontend/thanh-minh-smart-pool/src/features/safety/utils/cameraRiskFrameBuffer.js), MAX_UNOBSERVED_TICKS và history.

Camera vẫn có các detection mới cùng TrackId, box hợp lệ nhưng confidence 0,4. Engine bỏ qua quan sát này với ngưỡng mặc định 0,6, đồng thời vẫn giữ track trong ActiveTrackIds vì chưa chứng minh phục hồi. Tuy nhiên, frontend chỉ tìm RiskDecision cũ trong cửa sổ 300 ms.

Sau hơn 300 ms, box bị xóa dù:

- Người đó vẫn có detection/geometry mới liên tục.
- TrackId và trackingSession không đổi.
- Backend vẫn báo activeRiskTrackIds chứa người đó.

**Test:** F03 cấp detection mới mỗi 100 ms, tới mốc 500 ms kỳ vọng còn 1 box; thực tế 0. Khi confidence tốt trở lại, RiskDecision xuất hiện lại và box sáng lên, tạo nhấp nháy.

**Phân loại:** giới hạn thiết kế đã được xác nhận, trái yêu cầu box liên tục của người dùng. Test hiện có “carried boxes expire…” đang bảo vệ TTL 300 ms nên cần cập nhật cùng thay đổi thiết kế, không chỉ tăng TTL tùy ý.

**Hướng xử lý:** tách geometry/trạng thái đang nhìn thấy track khỏi quyết định thay đổi risk. Có thể giữ màu nguy cơ đã chốt trên geometry mới trong khi track còn được quan sát; khi không còn detection thực phải có TTL riêng, không kéo dài box vô hạn và không ghi detection confidence thấp thành bằng chứng cảnh báo mới.

## 7. F04 — SignalR giữ danh tính cũ khi đổi phiên đăng nhập

**Vị trí:** [signalrClient.js](../frontend/thanh-minh-smart-pool/src/services/signalrClient.js), getSignalRConnection và startSignalRConnection dòng 87; [authService.js](../frontend/thanh-minh-smart-pool/src/features/auth/services/authService.js), logout; [NotificationHubBase.cs](../backend/src/SmartPool.Infrastructure/Services/NotificationHubBase.cs), OnConnectedAsync.

connection là singleton. accessTokenFactory đọc token mới khi transport cần xác thực, nhưng startSignalRConnection trả true ngay nếu connection đã Connected. Logout chỉ xóa session, không stop connection. Các hook có identity dependency chỉ gỡ/gắn listener.

Trong cùng một SPA/tab không reload:

~~~text
Đăng nhập A → SignalR được xác thực A → server gán nhóm A
Đăng xuất → token local bị xóa, transport vẫn sống
Đăng nhập B → startSignalRConnection thấy Connected → không conn.start lại
  → các listener của B có thể đang dùng connection/claims/nhóm của A
~~~

Đổi role ADMIN ↔ LIFEGUARD hoặc đổi người dùng làm vấn đề rõ hơn: nhóm user và quyền SubscribeCameraAnalysis được quyết định từ principal trên connection. HTTP polling dùng token mới không sửa principal của một WebSocket cũ. Cũng cần xử lý refresh token có chủ đích để không làm mất subscription.

**Test:** F04 chạy source service thật với HubConnection transport giả, đăng nhập token-A rồi token-B. Kỳ vọng transport xác thực cả A và B; thực tế chỉ A. Test chứng minh thiếu reconnect, chưa phải một bài tấn công E2E hay bằng chứng dữ liệu đã bị lộ trên server đang chạy.

**Hướng xử lý:** quản lý vòng đời SignalR theo user identity; stop khi logout/đổi tài khoản; reconnect có kiểm soát khi cần thay token/claims, rồi tham gia lại các nhóm đang được subscribe. Xử lý đồng thời các lời gọi start để tránh đua khởi động.

## 8. F05 — Gửi metadata tuần tự làm các camera chặn nhau

**Vị trí:** [CameraAnalysisBroadcastWorker.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/CameraAnalysisBroadcastWorker.cs), foreach tại dòng 24 và timeout 200 ms.

Inference không bị SignalR chặn, nhưng broadcaster vẫn await từng camera theo thứ tự. Với 7 lần gửi trước bị timeout, camera khỏe cuối danh sách phải đợi khoảng 7 × 200 ms rồi mới được gọi gửi. Chu kỳ còn có delay 75 ms sau lượt.

**Test:** tạo 8 camera với frame mới; 7 destination đầu chờ cancellation, destination cuối trả ngay. Đo được khoảng **1.497 ms** trước khi gọi gửi camera khỏe, trong khi frontend MAX_OVERLAY_AGE_MS = 1.000 ms.

Số này là kết quả bài test áp lực mô phỏng, không phải khẳng định camera demo hiện đang có đúng độ trễ này. Với 3 camera, mức chờ do hai timeout trước vào khoảng 400 ms cộng chi phí còn lại; vẫn làm giảm nhịp metadata.

**Hướng xử lý:** coalesce theo camera và gửi song song có giới hạn; một camera không giữ slot của camera khác. Bảo đảm timeout và cancellation vẫn hữu hạn, tránh tạo vô hạn task gửi. Không cần tăng tuổi thọ box để che độ trễ ở broadcaster.

## 9. Các điểm cần chỉnh tiếp qua kiểm tra code

Các mục dưới đây chưa có test audit riêng trong lượt này; mức độ chắc chắn và điều kiện được ghi rõ.

### F06 — Camera offline có thể chiếm slot AI vô thời hạn (P2)

[LiveMonitoringWorker.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/LiveMonitoringWorker.cs), ExecuteAsync dòng 33–36: danh sách được OrderBy ID, chấp nhận camera có LastFrameAt hoặc AnalysisSession, sau đó Take(MaxLiveCameras).

Ví dụ giới hạn 4, bốn camera đứng đầu đã từng có frame nhưng mất kết nối lâu, camera thứ năm đang Connected. Bốn camera cũ vẫn có thể giữ cả bốn slot vì không có thời hạn “mất kết nối ngắn”; camera thứ năm không có RunCameraAsync. State Error/Connecting cũng có thể chiếm chỗ theo điều kiện hiện tại.

Giữ worker qua reconnect ngắn là hợp lý để tránh load model lại. Cần grace period và chính sách ưu tiên camera Connected; công khai camera nào chưa có slot AI thay vì chỉ cho thấy video vẫn phát.

### F07 — CANCEL chưa có kiểm tra mức mà người dùng đang nhìn (P2)

[AlertsController.cs](../backend/src/SmartPool.API/Controllers/AlertsController.cs), Cancel; [MonitoringRepository.cs](../backend/src/SmartPool.Infrastructure/Persistence/Repositories/MonitoringRepository.cs), HandleAsync dòng 56.

ACK đã so expectedSeverity, CANCEL thì không. Nếu popup còn hiển thị WARNING nhưng DB vừa nâng CRITICAL, nhấn Hủy có thể xử lý luôn CRITICAL mới mà không có bước cho người dùng thấy mức mới. Đây là cùng dạng dữ liệu màn hình cũ với lỗi ACK đã được xử lý trước đó.

Nếu nghiệp vụ chủ ý cho phép “hủy mức hiện tại bất kể mức đang hiển thị” thì cần thể hiện rõ. Với thao tác hiện nay, nên áp version/severity guard cho cả cancel và refresh màn hình khi conflict.

### F08 — Áp nóng bộ ngưỡng chưa nguyên tử với các engine đọc đồng thời (P2)

[RiskThresholds.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/RiskThresholds.cs), dòng 91/125; [RiskDecisionEngine.cs](../backend/src/SmartPool.Application/Features/SafetyMonitoring/RiskDecisionEngine.cs), CopyFrom.

Semaphore chỉ tuần tự hóa bên ghi. CopyFrom gán từng property của instance chung, trong khi các camera/video engine đọc instance đó mà không dùng cùng lock/snapshot. Một Observe có thể đọc lẫn trường cũ và mới khi admin lưu ngưỡng. Test hot reload hiện tại chạy tuần tự nên chưa chứng minh tính nhất quán đa luồng.

Nên publish một snapshot ngưỡng bất biến bằng hoán đổi tham chiếu; mỗi Observe lấy một snapshot duy nhất, giữ nguyên state/episode của engine. Đây là nguy cơ concurrency được suy ra từ code, chưa đo tần suất xảy ra khi vận hành.

### F09 — Gắn JPEG với RTP bằng FIFO chưa chứng minh đồng bộ nội dung (cần kiểm định)

[CameraAnalysisFramePump.cs](../backend/src/SmartPool.Infrastructure/Cameras/CameraAnalysisFramePump.cs), pending.Enqueue tại dòng 88 và TryDequeue tại dòng 75.

Một access unit đầu vào được gắn một mốc thời gian, mỗi JPEG đầu ra dequeue một mốc theo FIFO. Hợp đồng này giả định decoder ra đúng một ảnh theo cùng thứ tự, không reorder/drop. Raw image2pipe không mang PTS mà code đang đọc.

Test RTP và tọa độ nội suy đúng chưa chứng minh ảnh mà model nhìn chính là frame có RTP đó, đặc biệt với nguồn reorder, dữ liệu lỗi hoặc decoder bỏ frame. Không kết luận FIFO chắc chắn sai với ba clip baseline hiện tại; cần fixture video đánh số frame trong nội dung, so ảnh/model/metadata/video trình bày, và thêm nguồn có B-frame/corruption. Nếu dùng các nguồn này, nên giữ PTS từ decoder xuyên pipeline.

### F10 — Render ảnh ngay trong vòng LIVE làm tăng thời gian toàn vòng (cơ hội tối ưu)

[LiveMonitoringWorker.cs](../backend/src/SmartPool.Infrastructure/SafetyMonitoring/LiveMonitoringWorker.cs), dòng 139: mỗi frame có decision đều await RenderAsync trước state.Set, dù đang xem mode smooth và snapshot của episode đã tồn tại.

Điều này không sai về đồng bộ ảnh/box nhưng dùng CPU/IPC/encode ngay trên đường inference; InferenceMilliseconds lại chỉ đo InferAsync nên không phản ánh chi phí này. Cần đo thêm Render/whole-loop trước khi chọn tối ưu. Có thể render theo nhu cầu paired viewer hoặc snapshot, dùng cache phù hợp với endpoint frame; phải giữ ảnh bất biến đúng quan sát.

### F11 — Demo loop hiện vẫn đóng publisher (giới hạn đã biết)

[publish-stream.ps1](../tools/camera-simulator/publish-stream.ps1), dòng 25–36: chờ FFmpeg kết thúc rồi phát lượt mới. Backend tạo session simulator mới và reset tracker/risk. Vì vậy gián đoạn và ID mới tại ranh giới lượt là hệ quả cơ chế hiện tại; không phải tự động chứng minh tracker đổi sai giữa clip.

Nếu cần demo vòng liên tục, cần thiết kế loop không ngắt RTSP và tín hiệu mốc lượt riêng cho AI. Không bỏ reset một cách tùy ý vì sẽ kéo state/episode của người ở cuối clip sang đầu clip.

## 10. Code thừa hoặc nên đơn giản hóa

| Vị trí | Nhận xét | Đề xuất |
|---|---|---|
| PairedCameraPlayer.jsx (đã xóa sau sửa) | Trước sửa chỉ có khai báo, không có import/use. Cơ chế JPEG polling 500 ms cũ đã được thay bằng CameraVideoPlayer/WebRTC trên trang chính | Đã xóa. Không nhầm với CameraAnalysisVideoSource đang dùng thật |
| LiveMonitoringWorker.RunCameraAsync | Mỗi hàm chỉ xử lý 1 camera nhưng còn 5 dictionary theo Guid, mảng ids chỉ có cameraId và vòng dọn engines.Keys.Except(ids) | Đổi thành state cục bộ của một camera: engine, last, session, summaries. Bỏ vòng dọn không thể có ID khác, giữ semantics reset/reconnect |
| MonitoringContracts.RiskWriteResult.SnapshotRequired | Các nhánh RecordAsync đều trả false, source hiện tại không đọc thuộc tính này | Dấu vết hợp đồng “lưu Warning rồi mới chụp”. Có thể rút gọn kết quả/đổi tên khi cập nhật tests/caller cùng lúc |
| RiskOptions.Clone | Không thấy caller production qua tìm kiếm .Clone(); hiện chỉ là helper chưa sử dụng | Có thể dùng trong thiết kế snapshot mới hoặc bỏ nếu không có nhu cầu; mức ưu tiên thấp |
| SnapshotUploadWorker.RunLegacyAsync, CompleteSnapshotAsync, SnapshotAttempts/SnapshotStatus | Vẫn có code gọi thật để phục hồi Warning cũ; không được coi là dead code chỉ vì luồng mới image-first | Chỉ gỡ sau khi truy vấn DB xác nhận không còn bản ghi legacy cần phục hồi và có kế hoạch migration |

Không xóa LiveAlertPanel: MonitoringAlertsPage vẫn import và render component này. Không xóa luồng paired WebRTC: đây vẫn là fallback khi source/browser không hỗ trợ smooth/RTP.

## 11. Những phần đang hợp lý và nên giữ

- Mỗi camera có tracker/engine riêng; chọn JPEG mới nhất để không xếp backlog inference dài.
- Ghép RTP access unit và bộ nhớ channel có giới hạn; không drop P-frame tùy ý rồi tiếp tục decode sai.
- State và SignalR tách khỏi upload ảnh; outbox retry không ép ghi Warning thiếu ảnh.
- Chống trùng theo source/track/episode, advisory lock và khóa Warning để xử lý concurrency DB.
- ACK kiểm tra mức đang hiển thị; critical LIVE tạo Incident, WARNING/UPLOAD không tạo Incident.
- Ảnh của quan sát được giữ bất biến; upload retry dùng public ID ổn định cho item, tách khỏi ảnh Incident đã xử lý.
- Upload video có job/worker riêng, kiểm tra timeline và phân quyền, không phát popup cứu hộ toàn cục cho video offline.
- Phân biệt mất quan sát và phục hồi đã có bằng chứng; không tự biến “không thấy người” thành trạng thái an toàn.

## 12. Lệnh chạy lại

Chạy từ thư mục gốc, trừ lệnh frontend có đổi thư mục. Test hồi quy đã vào bộ chính và **kỳ vọng exit code 0** sau sửa.

~~~powershell
dotnet test backend/tests/SmartPool.UnitTests/SmartPool.UnitTests.csproj --no-restore

Push-Location frontend/thanh-minh-smart-pool
$safetyTests = @(rg --files src/features/safety -g '*.test.mjs' -g '*.test.js')
node --test @safetyTests
Pop-Location

python -m unittest discover -s backend/ai -p test_tracking.py -v

~~~

Integration chỉ chạy khi PostgreSQL test đã được bật; không trỏ tới database dev/vận hành:

~~~powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=15433;Database=smartpool_test;Username=postgres'
dotnet test backend/tests/SmartPool.IntegrationTests/SmartPool.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~SmartPool.IntegrationTests.SafetyMonitoring.SafetyMonitoringTests'
~~~

Native test cần các biến SMARTPOOL_SAFETY_TEST_PYTHON/FFPROBE/FONT và SMARTPOOL_CAMERA_TEST_FFMPEG trỏ tới file thật; thiếu thì xUnit skip. Không bật Cloudinary test bằng thông tin dịch vụ thật chỉ để làm tổng số test xanh.

Build ban đầu còn phát warning NU1903 cho Microsoft.Extensions.Caching.Memory 8.0.0 trong Application, kèm advisory GHSA-qj66-m88j-hmgj. Đây là cảnh báo dependency quan sát được trong log, tách khỏi năm finding audit phía trên; chưa nâng package trong lượt review này.

## 13. Thứ tự xử lý đề nghị

1. F01/F02: tránh bỏ lọt mức Khẩn cấp ở popup và hàng đợi ảnh.
2. F04: ràng buộc SignalR vào đúng phiên đăng nhập.
3. F03/F05: tách geometry khỏi risk, bỏ chặn tuần tự giữa các camera.
4. F06/F07/F08: chính sách cấp slot, stale cancel và ngưỡng nguyên tử.
5. Kiểm định F09 bằng frame có nội dung đánh số, đo F10; xử lý seam simulator nếu cần demo liên tục.
6. Dọn code mục 10 sau khi test hồi quy phù hợp đã đạt.
