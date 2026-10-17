# Thêm thiết bị Camera và kiểm tra kết nối

## Kết quả và phạm vi

Nhóm có tên `Camera` (không phân biệt hoa/thường) mở thêm cấu hình kết nối trong modal Thêm thiết bị. Các nhóm khác tiếp tục dùng luồng thêm thiết bị đã có.

Hiện hỗ trợ **RTSP qua TCP, video H.264/MJPEG**. Không cung cấp lựa chọn HTTP, ONVIF, RTSPS hoặc H.265 vì chưa triển khai các giao thức/codec này. Không tự kích hoạt AI.

Trước khi triển khai, codebase chỉ có `Warning.CamId` liên kết `Equipment` và một dòng CameraHub đang comment. Không tìm thấy module đọc video, dịch vụ camera bên ngoài, stream manager hoặc thuật toán AI để tích hợp trực tiếp. Phần bổ sung cung cấp một dịch vụ RTSP dùng chung trong tiến trình và các điểm lấy trạng thái/frame cho module giám sát hoặc AI được triển khai sau này; không tạo dashboard giám sát hay thuật toán nhận diện mới.

Không thay đổi database schema, không tạo bảng camera hoặc entity thiết bị trùng. Cấu hình nằm trong cột JSONB `equipments.metadata` hiện có.

## Form

- Chọn/nhập nhóm `Camera` để hiện RTSP URL, giao thức RTSP (TCP), username và password tùy chọn.
- RTSP URL bao gồm IP/hostname, cổng (mặc định 554), đường dẫn và query nếu camera cần. Không nhúng username/password vào URL; sử dụng trường riêng.
- Mật khẩu dùng input password không bật nút hiện văn bản. Không dùng localStorage/sessionStorage để lưu cấu hình camera.
- Khi thất bại, giữ tên, nhóm, vị trí, URL, username và thông số; xóa password khỏi form để nhập lại. Không đóng modal, không báo thành công.
- Loading dùng nhãn “Đang kiểm tra camera...”, giữ cơ chế khóa form và chặn submit trùng.
- Chỉ sau khi backend đọc được frame và lưu thành công mới báo “Thêm thiết bị và kết nối camera thành công”.
- Danh sách tải lại từ backend; thiết bị Camera hiển thị thêm trạng thái kết nối. Khi trang có Camera, tự tải lại mỗi 10 giây.
- Quy tắc nhóm trước đó giữ nguyên: chỉ nhập nhóm đầu tiên khi chưa có nhóm; khi đã có nhóm phải chọn dropdown thực tế. Không tự chèn nhóm Camera vào danh mục nếu dữ liệu hiện có chưa có nhóm này.

## Request và API

Tiếp tục dùng `POST /api/equipments`, policy `AdminOnly` và JWT hiện có. Bổ sung field `cameraConnection` chỉ dành cho nhóm Camera:

```json
{
  "name": "Tên camera",
  "category": "Camera",
  "location": null,
  "metadata": null,
  "cameraConnection": {
    "protocol": "RTSP",
    "streamUrl": "rtsp://<camera-host>:554/<stream-path>",
    "username": null,
    "password": null
  }
}
```

Các placeholder phải được thay bằng cấu hình camera thực tế; không phải địa chỉ kết nối mẫu đã được kích hoạt.

- Thành công: `201 Created`, DTO thiết bị có `cameraConnection: { state, lastFrameAt }`.
- Cấu hình/địa chỉ ngoài phạm vi: `400`.
- Camera trùng/đang được đăng ký: `409`.
- Không thể xác thực, kết nối hoặc đọc frame: `422`.
- Thiếu FFmpeg hoặc hết giới hạn kết nối: `503`.
- Lỗi lưu ngoài dự kiến: `500`, thông báo chung, không trả chi tiết nội bộ.
- Chưa đăng nhập/không phải Admin tiếp tục trả `401`/`403`.

Hai điểm tích hợp chỉ dành cho Admin:

- `GET /api/equipments/{id}/camera`: trạng thái và thời điểm frame hợp lệ gần nhất.
- `GET /api/equipments/{id}/camera/frame`: JPEG mới nhất đã được giải mã, `Cache-Control: no-store`; trả `503` nếu mất kết nối/frame hết hạn/thiết bị không Active, `404` nếu không phải camera tồn tại.

`ICameraService.GetFrame` và `GetStatus` là điểm dùng chung cho module chạy trong backend. Đọc frame không mở thêm kết nối RTSP. API frame cung cấp ảnh chụp cập nhật, không phải HLS/WebRTC/video đầy đủ; giao diện xem video và tích hợp AI cụ thể vẫn cần module tương ứng.

## Kết nối và transaction

1. Handler kiểm tra dữ liệu thiết bị/nhóm và cấu hình camera.
2. Kiểm tra endpoint trong allowlist và chuẩn hóa định danh camera bằng hash của **IP đã pin + cổng**. Các đường dẫn stream khác nhau trên cùng IP/cổng được coi là cùng một camera. Đây là quy tắc chống trùng được bổ sung vì chưa có định danh camera trước đó.
3. Kiểm tra database có camera này chưa, trước khi mở RTSP.
4. Camera service đặt chỗ endpoint, giới hạn tổng số kết nối; mở RTSP qua TCP, xác thực và nhận dữ liệu video.
5. H.264 keyframe (kèm SPS/PPS) hoặc MJPEG frame được đưa vào FFmpeg qua stdin. Chỉ chấp nhận sau khi FFmpeg giải mã được JPEG hợp lệ. Không lấy kết quả “cổng mở” hoặc “RTSP trả 200” làm xác nhận kết nối thành công.
6. Sau bước mạng mới mở transaction PostgreSQL ngắn. Dùng advisory transaction lock theo định danh để kiểm tra chống trùng lần cuối giữa các request/API instance, thêm một bản ghi `Equipment` và publish worker trong bộ nhớ; không mở kết nối mạng trong transaction.
7. Nếu save/commit/publish thất bại, rollback database và dispose registration: đóng socket, dừng decode, xóa chỗ giữ/stream. Không để stream được publish gắn với một thiết bị chưa commit.
8. Khi commit thành công, giữ lại chính kết nối đã kiểm tra làm kết nối dùng chung; không mở thêm một RTSP session riêng chỉ để đọc frame cho giám sát.

`status = Active` và `is_deleted = false` do backend gán sau khi xác minh được frame. Trạng thái kết nối tách riêng: `Connecting`, `Connected`, `Disconnected`, `Error`.

Worker giải mã keyframe tối đa khoảng một lần mỗi 2 giây, giữ JPEG gần nhất trong bộ nhớ. Nếu frame hết hạn, API không trả ảnh cũ như một camera đang hoạt động. Khi socket mất kết nối hoặc xác thực lỗi, ghi sự kiện với ID/mã lỗi an toàn và thử kết nối lại theo cấu hình; không đổi `equipments.status`.

Supervisor khôi phục camera Active từ cấu hình đã mã hóa khi khởi động và đồng bộ mỗi 20 giây. Thiết bị bị xóa/ngừng Active được gỡ khỏi worker khi đồng bộ. Quản lý kết nối dùng chung nằm trong một tiến trình backend; triển khai nhiều backend cần thiết kế chủ sở hữu stream dùng chung ngoài tiến trình, không coi mỗi instance là một stream manager tập trung.

## Lưu bí mật và giới hạn mạng

- Metadata có envelope `_camera` chứa phiên bản, hash định danh và `protectedConfiguration`; URL, username và password được mã hóa bằng ASP.NET Core Data Protection.
- Thông số thiết bị vẫn ở `specifications`; không lưu password/credentials/URL nhúng thông tin đăng nhập trong phần thông số công khai. Client không được tự gửi trường `_camera`.
- API create/list chỉ trả thông số công khai và trạng thái. Không trả URL, username/password hoặc ciphertext của cấu hình đã lưu.
- Không log request body, RTSP exception text, frame hoặc stderr từ FFmpeg. Lỗi camera được chuyển thành mã/thông báo cố định; logger chỉ dùng ID và mã lỗi. Lỗi database khi thêm camera không được log toàn bộ exception vì có thể chứa dữ liệu cấu hình.
- Windows: key ring được bảo vệ bằng DPAPI của tài khoản chạy service. Linux: khi bật camera phải cấu hình certificate bảo vệ key ring. Cần giữ key ring và tài khoản/certificate ổn định sau khi triển khai lại để giải mã cấu hình đã lưu.
- Mặc định allowlist rỗng: không mở kết nối camera nào. Chỉ chấp nhận đúng hostname/IP và cổng do quản trị server cấu hình.
- Hostname phải có danh sách IP được pin; mọi kết quả DNS phải thuộc danh sách đó. Kết nối thực tế dùng IP đã kiểm tra, tránh DNS thay đổi giữa kiểm tra và kết nối.
- Không dùng UDP, không theo RTSP redirect sang địa chỉ mới, không chấp nhận HTTP/file/URL có userinfo hay địa chỉ metadata link-local. Redirect sang endpoint không được phép đã được kiểm thử bằng máy chủ RTSP thật.
- FFmpeg chỉ nhận dữ liệu H.264/MJPEG qua pipe, giới hạn protocol `pipe`; không nhận URL/mật khẩu hoặc được phép tự mở giao thức mạng.

## Cấu hình vận hành

Các khóa mẫu đã thêm vào `backend/src/SmartPool.API/.env.example`. Chưa sửa `.env` đang dùng và chưa đặt địa chỉ camera thực tế.

```text
CameraSettings__AllowedEndpoints__0__Host=<camera-ip-or-hostname>
CameraSettings__AllowedEndpoints__0__Port=<camera-port>
CameraSettings__AllowedEndpoints__0__Addresses__0=<pinned-ip-if-using-hostname>
CameraSettings__FfmpegPath=<absolute-path-to-ffmpeg-executable>
CameraSettings__KeyRingPath=<persistent-protected-key-directory>
```

Mặc định timeout kết nối 15 giây, frame hết hạn sau 15 giây, thử lại sau 5 giây, tối đa 16 kết nối. Nếu camera có khoảng cách giữa các H.264 keyframe lớn, cấu hình `FrameStaleSeconds` phù hợp (giới hạn 3–60 giây). Cấu hình cổng/hostname/IP, key ring và FFmpeg trước khi khởi động lại backend.

FFmpeg là dependency triển khai bên ngoài, không tự tải/cài trên server khi người dùng nhấn Lưu. Binary dùng cho kiểm thử chỉ đặt trong `_notes/camera-tools`, không đóng gói vào API và không ghi đường dẫn thử nghiệm vào cấu hình ứng dụng.

## File mới

- `backend/src/SmartPool.Application/Interfaces/Services/ICameraService.cs`
- `backend/src/SmartPool.Application/Features/manage-equipments/CameraMetadata.cs`
- `backend/src/SmartPool.Infrastructure/Cameras/CameraOptions.cs`
- `backend/src/SmartPool.Infrastructure/Cameras/CameraAddressPolicy.cs`
- `backend/src/SmartPool.Infrastructure/Cameras/CameraFrameDecoder.cs`
- `backend/src/SmartPool.Infrastructure/Cameras/CameraWorker.cs`
- `backend/src/SmartPool.Infrastructure/Cameras/CameraService.cs`
- `backend/src/SmartPool.Infrastructure/Cameras/CameraSupervisor.cs`
- `backend/tests/SmartPool.UnitTests/Features/ManageEquipments/CameraEquipmentTests.cs`
- `backend/tests/SmartPool.IntegrationTests/CameraEquipmentTests.cs`
- `backend/tests/SmartPool.IntegrationTests/LocalRtspCamera.cs`
- `docs/camera-registration.md`

## File cập nhật trong lần mở rộng Camera

- `backend/src/SmartPool.API/Controllers/EquipmentsController.cs`
- `backend/src/SmartPool.API/.env.example`
- `backend/src/SmartPool.Application/Interfaces/Repositories/IEquipmentOperations.cs`
- `backend/src/SmartPool.Application/Features/manage-equipments/Commands/CreateEquipment/CreateEquipmentCommand.cs`
- `backend/src/SmartPool.Application/Features/manage-equipments/Commands/CreateEquipment/CreateEquipmentHandler.cs`
- `backend/src/SmartPool.Application/Features/manage-equipments/Queries/GetEquipments/GetEquipmentsQuery.cs`
- `backend/src/SmartPool.Application/Features/manage-equipments/Queries/GetEquipments/GetEquipmentsHandler.cs`
- `backend/src/SmartPool.Infrastructure/Persistence/Repositories/EquipmentOperations.cs`
- `backend/src/SmartPool.Infrastructure/DependencyInjection.cs`
- `backend/src/SmartPool.Infrastructure/SmartPool.Infrastructure.csproj`: thêm RtspClientSharp 1.3.3.
- `frontend/thanh-minh-smart-pool/src/features/equipments/components/EquipmentFormModal.jsx`
- `frontend/thanh-minh-smart-pool/src/features/equipments/hooks/useCreateEquipment.js`
- `frontend/thanh-minh-smart-pool/src/features/equipments/hooks/useEquipments.js`
- `frontend/thanh-minh-smart-pool/src/pages/admin/EquipmentPage.jsx`
- `docs/add-equipment.md`

## Kiểm thử đã chạy

- Build backend: đạt; còn các cảnh báo nullable từ những phần có sẵn.
- Build và lint frontend: đạt; còn cảnh báo kích thước bundle và static message của Ant Design như trước.
- Toàn bộ unit test: **112/112 đạt**, gồm validation cấu hình, địa chỉ ngoài allowlist, cổng sai, giao thức sai, URL nhúng credentials, hostname chưa pin, mặc định deny, kiểm tra dispose khi save thất bại và ngăn thông tin xác thực trong thông số công khai.
- Integration tests lọc `FullyQualifiedName~Equipment`: **8/8 đạt** trên PostgreSQL kiểm thử riêng. Có 5 bài RTSP: video H.264 thật và JPEG giải mã thật; cấu hình lưu mã hóa/chống trùng; sai mật khẩu và video lỗi; redirect không mở endpoint khác; khôi phục kết nối dùng chung; lỗi database rollback và giải phóng registration. Các bài danh sách/tìm kiếm/lọc/phân trang và HTTP thêm thiết bị trước đó vẫn đạt.
- Chrome headless → frontend thật → API JWT thật → RTSP loopback phát H.264 → FFmpeg giải mã → PostgreSQL: đạt. Kiểm tra phần Camera chỉ hiện đúng nhóm, input password, sai mật khẩu không tạo thiết bị và xóa password, lưu lại đúng thông tin, thông báo thành công sau 201, không lộ credentials trong response, đọc JPEG qua endpoint dùng chung, chống trùng, mất kết nối trả 503 cho frame.
- Dịch vụ/browser/database fixture kiểm thử đã dọn; không thêm camera hoặc dữ liệu thử vào database ứng dụng, không bật AI, không restart backend đang chạy của người dùng.

Máy chủ RTSP thử nghiệm là fixture nội bộ phát bitstream video thật, không phải kết quả kết nối giả của ứng dụng. Chưa xác minh với camera vật lý tại bể bơi vì chưa nhận được IP/hostname, cổng và đường dẫn RTSP thực tế. Chưa có module giám sát/AI bên ngoài để kiểm thử tích hợp trực tiếp.

## Tài liệu kỹ thuật đối chiếu

- [API và codec hỗ trợ của RtspClientSharp](https://github.com/BogdanovKirill/RtspClientSharp).
- [FFmpeg protocols và protocol whitelist](https://ffmpeg.org/ffmpeg-protocols.html).
- [Cấu hình ASP.NET Core Data Protection và key encryption](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-8.0).
