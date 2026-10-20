# Xem Camera trong danh sách thiết bị

Trong Quản lý thiết bị, cột Thao tác hiển thị **Xem Camera** khi `equipments.category` có giá trị `Camera` (trim, không phân biệt hoa/thường). Đây là quy ước nhóm đã dùng khi đăng ký camera; database chưa có bảng/mã ID nhóm riêng. Không hardcode ID và không tạo trang chi tiết. Tìm kiếm, bộ lọc và phân trang giữ nguyên.

Modal hiển thị tên thiết bị, trạng thái và video HLS. HLS có độ trễ vài giây, không phải WebRTC. Trong lúc khởi tạo/buffering hiện “Đang kết nối camera...”. Camera offline có thông báo “Camera hiện không khả dụng” và nút Thử lại; lỗi video và thiết bị không tồn tại được thông báo riêng. Khi lỗi hoặc mất kết nối, player bị hủy, nguồn video bị xóa và hình cũ bị ẩn. Modal đóng sẽ hủy yêu cầu đang chờ, polling trạng thái, player và phiên xem phía server.

## Luồng video dùng chung

`CameraWorker` vẫn giữ một RTSP/TCP connection cho mỗi endpoint. Bộ ghép NAL nhận H.264/H.265/MJPEG từ worker này và cung cấp video đầy đủ cho gateway HLS; kênh ảnh JPEG/keyframe cũ vẫn hoạt động độc lập.

`CameraStreamService` quản lý các phiên xem. Mỗi camera có tối đa một FFmpeg chuyển mã dùng chung cho các viewer của camera đó. Gateway đợi keyframe rồi nhận encoded bytes qua stdin, chuyển sang H.264/yuv420p, tối đa chiều rộng 1280, 15 fps, hai luồng encoder; HLS có segment khoảng một giây và playlist cuốn chiếu bốn segment. FFmpeg không nhận URL hoặc credentials và chỉ được đọc protocol `pipe`. Gateway không tạo kết nối RTSP, không khởi động hay dừng AI.

Các file HLS được tạo trong thư mục tạm riêng do server sinh, không nằm trong static web root. Tên file được giới hạn thành `index.m3u8` hoặc `segment_000000000.ts`; không nhận đường dẫn tùy ý. Đóng viewer cuối sẽ dừng encoder và xóa thư mục của phiên đó, nhưng giữ worker RTSP và ảnh JPEG. Trình duyệt đóng bất thường được dọn bằng TTL 35 giây, quét mỗi 5 giây. Tối đa 64 viewer cho tiến trình; giới hạn camera vẫn theo `CameraSettings:MaxConnections` hiện có. Queue video có giới hạn, không chặn worker khi chuyển mã chậm; phiên xem lỗi có thể thử lại.

## API

Tất cả endpoint sau sử dụng chính sách `AdminOnly`, JWT trong `Authorization` header và `Cache-Control: no-store`:

| Endpoint | Chức năng |
| --- | --- |
| `POST /api/equipments/{id}/camera/views` | Mở phiên, đợi playlist sẵn sàng (tối đa 25 giây), trả `viewId` và URL playlist tương đối của đúng thiết bị |
| `GET /api/equipments/{id}/camera/views/{viewId}/index.m3u8` | Playlist HLS |
| `GET /api/equipments/{id}/camera/views/{viewId}/segment_000000000.ts` | Segment H.264 của camera tương ứng |
| `DELETE /api/equipments/{id}/camera/views/{viewId}` | Đóng phiên xem; idempotent |
| `GET /api/equipments/{id}/camera` | API trạng thái hiện có, Modal kiểm tra mỗi giây |

Mở/đọc luồng kiểm tra thiết bị tồn tại, chưa xóa, thuộc nhóm Camera và đang Active. Thiết bị không tồn tại/đã xóa trả 404, sai nhóm trả 400, camera offline trả 503, phiên xem đã kết thúc hoặc gắn ID khác trả 410. Playlist/segment không có trả 404. Backend không trả mật khẩu hoặc RTSP URL. hls.js gửi JWT trong header cho cả playlist và segment; token không nằm trong URL. Không sửa database schema.

## Triển khai

Cần FFmpeg có decoder cho codec nguồn và encoder `libx264`, muxer HLS. Tái sử dụng `CameraSettings:FfmpegPath`, allowlist và key ring trong [cấu hình camera](camera-registration.md). Gateway ghi các segment tạm nên tài khoản backend cần quyền ghi thư mục temp. Sau khi cập nhật mã, khởi động lại backend và frontend. Không cần dịch vụ streaming bên ngoài.

## Kiểm thử

- Unit tests: 212 bài đạt. Integration tests thiết bị: 16 bài đạt trên PostgreSQL riêng.
- HLS nhận H.265 màu xanh và H.264 màu đỏ qua hai RTSP server kiểm thử; giải mã segment và kiểm tra màu để xác minh đúng camera. Nhiều viewer không tăng số kết nối RTSP; đóng viewer không dừng ảnh JPEG; đường dẫn file và ID chéo bị từ chối; mất kết nối không trả playlist cũ.
- HTTP kiểm tra JWT/Admin cho mở/đọc/đóng, từ chối nhóm khác, camera offline và camera đã xóa. Các bài tìm kiếm/lọc/phân trang hiện có vẫn đạt.
- Chrome headless → frontend thật → API JWT thật → EZVIZ H.265 thật → gateway HLS: video phát ở 1280×720, thời gian phát tăng liên tục; đóng Modal gửi DELETE 204, phiên cũ trả 410 trong khi ảnh JPEG vẫn trả 200. Mở lại rồi ngắt worker trong API kiểm thử: Modal báo offline, xóa `src` và ẩn video; Thử lại khi nguồn chưa phục hồi hiển thị lỗi đúng. Dùng database kiểm thử riêng và dọn dữ liệu sau kiểm tra.
- FFmpeg được giới hạn thời gian dò luồng (`analyzeduration=1`, `probesize=32768`, `fpsprobesize=0`) để khởi tạo kịp thời với nguồn H.265 có ít dữ liệu. Bản FFmpeg dùng thực tế phải hỗ trợ các tùy chọn này.

Tài liệu đối chiếu: [FFmpeg HLS muxer](https://ffmpeg.org/ffmpeg-formats.html#hls-2), [hls.js](https://github.com/video-dev/hls.js).
