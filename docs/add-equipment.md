# Thêm thiết bị

Phần mở rộng dành cho nhóm Camera được mô tả trong [Đăng ký và kết nối camera](camera-registration.md). Quy tắc và kiểm thử của phần Camera bổ sung cho luồng thiết bị thông thường bên dưới.

## Phạm vi

Tích hợp nút **Thêm thiết bị** và modal vào `/admin/equipment`. Tái sử dụng bố cục quản trị, route Admin, Axios client, TanStack Query, Ant Design, entity `Equipment`, generic repository và Unit of Work. Không thêm migration, bảng, chức năng sửa/xóa/ngừng sử dụng hoặc lịch bảo trì.

## Dữ liệu và quy tắc nhóm đã xác nhận

`smart_pool.equipments` có `name` tối đa 255 ký tự, `category` tối đa 50, `location` nullable tối đa 255, `metadata` nullable kiểu JSONB. Nhóm và vị trí là chuỗi, không phải khóa ngoại; không có bảng nhóm riêng.

Theo xác nhận của người dùng:

- Khi chưa có nhóm từ thiết bị chưa bị xóa: form cho nhập tên nhóm đầu tiên. Nhóm được lưu trong bản ghi thiết bị, không tạo bản ghi danh mục riêng.
- Khi đã có nhóm: form hiển thị dropdown từ `GET /api/equipments/filters`, không cho nhập nhóm mới.
- Backend kiểm tra lại nhóm có tồn tại; chỉ chấp nhận nhóm mới nếu chưa có nhóm nào. Nếu dữ liệu nhóm thay đổi sau khi mở form, trả lỗi tại trường nhóm để tải lại danh mục hoặc mở lại form.
- Không hardcode nhóm, không thêm dữ liệu mẫu vào database ứng dụng.

## API

`POST /api/equipments`, dùng policy `AdminOnly` và middleware JWT hiện có.

Body gồm:

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `name` | string | Bắt buộc, trim, tối đa 255 ký tự |
| `category` | string | Bắt buộc, trim, tối đa 50 ký tự; áp dụng quy tắc nhóm ở trên |
| `location` | string hoặc null | Không bắt buộc; trim; rỗng thành null; tối đa 255 ký tự |
| `metadata` | JSON hoặc null | Không bắt buộc; lưu JSON hợp lệ vào JSONB; không áp đặt một cấu trúc chung cho các nhóm |

Không nhận `status`, `isDeleted`, ID hoặc thời gian từ client. Backend tự tạo ID, thời gian UTC, `status = Active`, `is_deleted = false`. Không áp đặt tên duy nhất.

Controller → MediatR Handler → `IRepository<Equipment>` → `IUnitOfWork.SaveChangesAsync` → PostgreSQL. EF Core xử lý transaction của SaveChanges. Chỉ thêm một thiết bị, không tạo lịch bảo trì hay sửa thiết bị khác.

Thành công trả `201 Created` và `EquipmentListItem` hiện có. Validation trả `400` với `message` và dictionary `errors`. Lỗi lưu ngoài dự kiến được ghi log phía server, trả thông báo chung **Thêm thiết bị thất bại**, không trả chi tiết database cho client. Chưa đăng nhập trả `401`, không phải Admin trả `403`.

## Form và làm mới danh sách

- Hai trường bắt buộc: tên và nhóm. Vị trí và thông số là tùy chọn.
- Chưa có cấu hình form thông số theo nhóm nên dùng textarea JSON có validation trước khi gửi; gửi metadata dưới dạng JSON thực, không dùng chuỗi JSON lồng thêm một lớp. Bỏ trống hoặc nhập JSON null được lưu null.
- Lỗi backend được gắn vào các trường tương ứng; lỗi mạng/lưu có thông báo chung, giữ dữ liệu để thử lại.
- Loading khóa các trường, Hủy và đóng modal; ref chặn gửi nhiều request ngay cả trước lượt render tiếp theo. Mutation không tự retry POST.
- Hủy xóa nội dung form và đóng, không gọi API tạo.
- Chỉ sau khi POST thành công mới thông báo **Thêm thiết bị thành công**, đóng và reset form.
- Invalidate các query `equipments` và `equipment-filters` để tải lại từ backend. Không chèn bản ghi giả và không đổi điều kiện tìm kiếm/lọc/phân trang hiện có. Thiết bị không khớp điều kiện có thể không xuất hiện ở trang đang xem, nhưng có thể tìm được qua API danh sách.

## File mới

- `backend/src/SmartPool.Application/Features/manage-equipments/Commands/CreateEquipment/CreateEquipmentCommand.cs`
- `backend/src/SmartPool.Application/Features/manage-equipments/Commands/CreateEquipment/CreateEquipmentHandler.cs`
- `backend/tests/SmartPool.UnitTests/Features/ManageEquipments/CreateEquipmentTests.cs`
- `frontend/thanh-minh-smart-pool/src/features/equipments/components/EquipmentFormModal.jsx`
- `frontend/thanh-minh-smart-pool/src/features/equipments/hooks/useCreateEquipment.js`
- `docs/add-equipment.md`

## File chỉnh sửa trong lần triển khai này

- `backend/src/SmartPool.API/Controllers/EquipmentsController.cs`: thêm POST và xử lý lỗi.
- `backend/tests/SmartPool.IntegrationTests/EquipmentApiTests.cs`: thêm kiểm thử POST bằng JWT và lưu dữ liệu thực.
- `frontend/thanh-minh-smart-pool/src/features/equipments/services/equipmentService.js`: thêm hàm create.
- `frontend/thanh-minh-smart-pool/src/pages/admin/EquipmentPage.jsx`: nút thêm và modal.
- `docs/UC8.1-equipment-list.md`: liên kết đến chức năng mới.

## Kiểm tra đã thực hiện

- `npm.cmd run build`: thành công; còn cảnh báo bundle lớn hiện có.
- `npm.cmd run lint`: thành công.
- Toàn bộ `SmartPool.UnitTests`: **100/100 đạt**. Các trường hợp mới bao gồm dữ liệu thiếu/trắng/null, giới hạn độ dài, ký tự null không hợp lệ với PostgreSQL, trim, metadata/null, mặc định hệ thống, nhóm chưa tồn tại, cho nhập nhóm đầu tiên, lỗi SaveChanges và không lộ chi tiết lỗi lưu.
- Integration tests lọc `FullyQualifiedName~Equipment`: **3/3 đạt**, dùng PostgreSQL `smartpool_test` riêng. HTTP test xác nhận POST 401/403/400/201, lưu tối thiểu/đầy đủ, JSONB, tên trùng được phép, không nhận system fields do client gửi thêm, không tạo lịch bảo trì, tìm được bản ghi qua danh sách; các test tìm kiếm/lọc/phân trang/bảo trì trước đó vẫn qua.
- Chrome headless chạy frontend thật, API thật và PostgreSQL kiểm thử riêng: mở form, dữ liệu bắt buộc/trắng, JSON sai không gửi POST, Hủy không gửi POST, giữ dữ liệu khi mô phỏng lỗi mạng, lưu lại thành công, chặn submit liên tiếp khi request đang xử lý, kiểm tra dữ liệu đã lưu và danh sách tải lại. Tạo nhóm đầu tiên bằng input rồi thêm thiết bị thứ hai bằng dropdown đã được xác minh.
- Modal được mở và thao tác ở viewport desktop và mobile 390px.
- Dịch vụ kiểm thử đã dừng; dữ liệu thử đã dọn khỏi database kiểm thử. Không chèn dữ liệu vào database ứng dụng và không khởi động lại backend người dùng đang chạy.

Các công cụ và ảnh kiểm thử trình duyệt nằm trong `D:/Smart_pool/_notes/equipment-browser/`; host kiểm thử nằm trong `D:/Smart_pool/_notes/equipment-browser-host/`. Không thêm dependency Playwright vào frontend dự án.

## Lưu ý vận hành

- Khởi động lại backend hiện có để tải endpoint mới.
- Theo quy tắc người dùng đã chốt, khi đã có nhóm, chức năng này chỉ chọn nhóm hiện có. Việc bổ sung thêm nhóm khác sau đó không thuộc phạm vi đã yêu cầu.
- Không chạy toàn bộ integration test của các nghiệp vụ không liên quan. Các cảnh báo nullable hiện có và cảnh báo Ant Design về static message được ghi nhận; không sửa các phần ngoài phạm vi.
