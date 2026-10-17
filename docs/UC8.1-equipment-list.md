# UC 8.1 – Xem danh sách thiết bị

Chức năng thêm thiết bị được triển khai tiếp trong [Thêm thiết bị](add-equipment.md). Các mô tả phạm vi dưới đây ghi nhận riêng UC 8.1.

## Phạm vi và dữ liệu

Trang `/admin/equipment` dành cho Admin, sử dụng menu và bố cục quản trị hiện có.
Không triển khai thêm/sửa/ngừng sử dụng thiết bị hay chức năng bảo trì khác.
Không có migration, không sửa cấu trúc hoặc dữ liệu của database ứng dụng.

Schema đã đối chiếu trực tiếp bằng giao dịch chỉ đọc:

- `smart_pool.equipments`: khóa chính `id`; nhóm lưu trong `category`, vị trí trong `location`, thông số trong `metadata` kiểu `jsonb`.
- Không có entity/bảng nhóm thiết bị hoặc khu vực riêng trong mô hình hiện có.
- `smart_pool.maintenance_schedules.equipment_id` tham chiếu `equipments.id`.
- `smart_pool.maintenance_logs.schedule_id` liên kết lịch với lần bảo trì đã thực hiện.
- `status` thiết bị và lịch là chuỗi; giá trị mặc định lần lượt là `Active` và `Pending`.
- Tại thời điểm kiểm tra, hai bảng thiết bị và lịch bảo trì đều trống. Không thêm dữ liệu mẫu vào database ứng dụng.

## API

`GET /api/equipments`

Tham số: `searchTerm`, `category`, `location`, `status`, `pageIndex` (mặc định 1), `pageSize` (mặc định 10, tối đa 100).

Response dùng `PagedResponse<EquipmentListItem>` hiện có: `items`, `totalCount`, `pageIndex`, `pageSize`, `totalPages`.
Mỗi phần tử gồm `id`, `name`, `category`, `location`, `metadata`, `status`, `nextMaintenanceDate`.

`GET /api/equipments/filters`

Trả về `categories`, `locations`: các giá trị khác nhau từ thiết bị chưa bị xóa, không phụ thuộc trang/bộ lọc đang xem. `statuses` luôn có ba mã đã thống nhất: `Active` (Đang hoạt động), `Maintenance` (Bảo trì), `Inactive` (Ngừng sử dụng), kể cả khi database trống; bổ sung các mã khác nếu có trong dữ liệu để vẫn lọc được dữ liệu cũ.

Cả hai endpoint dùng policy `AdminOnly` hiện có, bao gồm kiểm tra đăng nhập và ID người dùng hợp lệ.
JWT tiếp tục được kiểm tra trạng thái tài khoản/vai trò hiện hành qua hạ tầng có sẵn.
Query không hợp lệ trả 400 với `message` và dictionary `errors` theo convention hiện tại.

## Backend

Controller → MediatR Handler (dịch vụ ứng dụng theo kiến trúc hiện có) → `IEquipmentOperations` → `EquipmentOperations` → PostgreSQL.

- Validator kiểm tra độ dài chuỗi, giới hạn trang và kích thước trang; tránh tràn số khi tính offset.
- Tìm gần đúng bằng PostgreSQL `ILIKE`, không phân biệt hoa/thường; escape `%`, `_` và `\` để tìm chúng như ký tự bình thường.
- Kết hợp các bộ lọc bằng AND; bỏ thiết bị có `IsDeleted = true`.
- `Count`, `OrderBy(Name).ThenBy(Id)`, `Skip`, `Take` và projection thực hiện trên database, dùng `AsNoTracking`.
- Lịch gần nhất là `MIN(planned_date)` từ lịch `Pending`, từ ngày hiện tại tại Việt Nam trở đi, chưa có maintenance log. Không tính lịch đã hoàn thành/hủy hoặc có log thực hiện.
- Một truy vấn đếm và một truy vấn danh sách có aggregate tương quan; không gọi database riêng cho từng thiết bị.
- Bộ lọc dùng ba truy vấn DISTINCT, số truy vấn không tăng theo số thiết bị.

## Frontend

- Route nằm trong `ProtectedRoute` chỉ cho `ADMIN`; menu có sẵn trong `AdminLayout` được tự highlight theo URL.
- Dùng Axios client, TanStack Query, Ant Design và header/style quản trị hiện có.
- Tìm kiếm debounce 350 ms; tìm kiếm hoặc đổi bộ lọc đưa về trang 1. Chuyển trang giữ điều kiện; đổi số dòng đưa về trang 1.
- Request cũ được hủy qua `AbortSignal`; query key bao gồm toàn bộ điều kiện.
- Thông số JSON được định dạng theo cấu trúc thực, không giả định các nhóm cùng trường; hiển thị hai dòng và tooltip đầy đủ.
- Bảng có cuộn ngang, bộ lọc xếp dọc trên màn hình nhỏ.
- Có loading, danh sách trống, không có kết quả, lỗi API/bộ lọc và nút Thử lại.

## File mới

- `backend/src/SmartPool.API/Controllers/EquipmentsController.cs`
- `backend/src/SmartPool.Domain/Enums/EquipmentStatuses.cs`
- `backend/src/SmartPool.Application/Interfaces/Repositories/IEquipmentOperations.cs`
- `backend/src/SmartPool.Application/Features/manage-equipments/Queries/GetEquipments/GetEquipmentsQuery.cs`
- `backend/src/SmartPool.Application/Features/manage-equipments/Queries/GetEquipments/GetEquipmentsHandler.cs`
- `backend/src/SmartPool.Infrastructure/Persistence/Repositories/EquipmentOperations.cs`
- `backend/tests/SmartPool.UnitTests/Features/ManageEquipments/GetEquipmentsTests.cs`
- `backend/tests/SmartPool.IntegrationTests/EquipmentListTests.cs`
- `backend/tests/SmartPool.IntegrationTests/EquipmentApiTests.cs`
- `frontend/thanh-minh-smart-pool/src/features/equipments/services/equipmentService.js`
- `frontend/thanh-minh-smart-pool/src/features/equipments/hooks/useEquipments.js`
- `frontend/thanh-minh-smart-pool/src/features/equipments/styles/equipmentManagement.css`
- `frontend/thanh-minh-smart-pool/src/pages/admin/EquipmentPage.jsx`
- Tài liệu này.

## File chỉnh sửa

- `backend/src/SmartPool.Infrastructure/DependencyInjection.cs`: đăng ký repository thiết bị.
- `frontend/thanh-minh-smart-pool/src/App.jsx`: thêm route Admin.

## Kiểm tra đã chạy

- `npm.cmd run build`: thành công (cảnh báo bundle lớn).
- `npm.cmd run lint`: thành công.
- Toàn bộ `SmartPool.UnitTests`: 86/86 thành công, gồm 8 trường hợp mới.
- Integration test lọc theo `FullyQualifiedName~Equipment`: 3/3 thành công trên PostgreSQL kiểm thử riêng, không ghi vào database ứng dụng.
- Repository tests: tìm theo tên/vị trí không phân biệt hoa/thường, escape wildcard, các bộ lọc độc lập/kết hợp, loại dữ liệu đã xóa, phân trang với tên trùng, metadata khác cấu trúc, lịch quá khứ/hoàn thành/hủy/đã có log, lịch hôm nay, không có lịch, không có kết quả.
- HTTP test chạy Kestrel với đăng ký dịch vụ và JWT thật: 401 khi chưa đăng nhập/tài khoản ngừng hoạt động, 403 với Sales, 200 với Admin, 400 cho query sai; tổng số bản ghi đối chiếu database.
- Chưa kiểm tra tương tác/hiển thị trực tiếp bằng trình duyệt; chưa chạy toàn bộ integration test của các nghiệp vụ khác.
- Build backend dùng thư mục artifacts riêng để không ghi đè backend đang chạy.

## Giới hạn cần xác nhận

1. Người dùng đã xác nhận quy ước `Active` / `Maintenance` / `Inactive`. Backend khai báo hằng chuỗi trong `EquipmentStatuses`, frontend dùng nhãn tiếng Việt tương ứng. Không thay đổi schema hoặc dữ liệu; đây là trạng thái thiết bị, không phải trạng thái lịch bảo trì.
2. `Pending` là trạng thái lịch chưa thực hiện duy nhất xác minh được. Nếu sau này có thêm trạng thái lịch đang mở, cần bổ sung sau khi xác nhận quy ước.
3. Không có chức năng chi tiết/cập nhật/ngừng sử dụng thiết bị hiện hữu. Cột Thao tác hiển thị dấu gạch, không tạo menu ba chấm hay liên kết giả.
4. Backend đang chạy trước khi sửa mã cần được khởi động lại để tải API mới.
