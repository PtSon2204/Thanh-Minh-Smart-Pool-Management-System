# Hướng dẫn áp dụng phiếu nhập hàng và chấm công nhân viên

## Áp dụng thay đổi cơ sở dữ liệu

Migration: `20261019063413_GoodsReceiptsAndStaffAttendance`.

Migration này dành cho cơ sở dữ liệu đã ở phiên bản `20261010090720_AddWarningAndToken`. Các thay đổi gồm:

- Tạo bảng `smart_pool.goods_receipts` và `smart_pool.goods_receipt_details`.
- Thêm liên kết `inventory_logs.receipt_detail_id` đến chi tiết phiếu nhập.
- Xóa bảng `salaries` và cột `employees.base_salary`.
- Xóa quyền `salaries` trong dữ liệu JSON phân quyền của các vai trò.
- Bổ sung ràng buộc dữ liệu chấm công.

**Sao lưu cơ sở dữ liệu, bao gồm bảng lương và lương cơ bản của nhân viên, trước khi áp dụng.** Khi chạy migration ngược, cấu trúc lương cũ được tạo lại nhưng dữ liệu lương đã xóa không được khôi phục. Ứng dụng không tự động chạy migration khi khởi động.

Thực hiện theo các bước sau:

1. Kiểm tra đúng cơ sở dữ liệu đích và bảng `__EFMigrationsHistory`, bảo đảm đã đáp ứng phiên bản migration nêu trên. `InitialCreate.Up()` trong repository hiện đang rỗng nên chuỗi migration này chưa thể khởi tạo toàn bộ cơ sở dữ liệu mới. Nếu trước đây tạo schema bằng SQL thủ công, cần đối chiếu với model cũ trước khi ghi nhận các migration là đã áp dụng.
2. Đọc và kiểm tra file `goods-receipts-and-attendance.sql`. Đây là script nâng cấp từ migration trước sang migration mới, có kiểm tra lịch sử để bỏ qua khi đã áp dụng; file này không tạo toàn bộ schema từ đầu.
3. Xử lý các phân công trùng bộ `(employee_id, shift_id, work_date)` và dữ liệu giờ ra không hợp lệ nếu đang tồn tại. Khi phát hiện các trường hợp này, migration sẽ dừng trước khi thay đổi cấu trúc; không tự xóa hoặc sửa các bản ghi đó.
4. Áp dụng script đã kiểm tra vào đúng cơ sở dữ liệu trong thời gian bảo trì, sau đó triển khai đồng bộ backend và frontend mới. Frontend cũ vẫn sử dụng trường lương nên không còn phù hợp với hợp đồng API mới.

Migration cũng tạo unique index cho phân công ca nếu index này chưa tồn tại. Các snapshot cũ đã khai báo index nhưng phần `Up()` của các migration cũ chưa tạo nó. Snapshot và file designer mới của EF đã khớp với model hiện tại.

## Nhập hàng và quản lý tồn kho

- Khi tạo sản phẩm, tồn kho ban đầu là `0` nếu có theo dõi tồn, hoặc `null` nếu không theo dõi. Hàng mua mới phải được ghi nhận qua phiếu nhập. Hệ thống không định giá lại tồn kho cũ và không tự suy đoán giá nhập chưa từng được lưu.
- `products.price` tiếp tục là giá bán hoặc giá thuê hiện hành. `unit_cost` là giá nhập của từng dòng phiếu; những lần nhập sau không ghi đè giá nhập cũ.
- Mỗi phiếu có từ 1 đến 100 sản phẩm khác nhau. Số lượng phải là số nguyên dương; giá nhập không âm và có tối đa hai chữ số thập phân.
- Phiếu nháp chưa làm thay đổi tồn kho. Khi xác nhận nhập kho, hệ thống khóa phiếu và cập nhật số lượng sản phẩm, lịch sử kho, trạng thái phiếu trong cùng một giao dịch. Gửi lại yêu cầu hoặc xác nhận đồng thời không làm cộng tồn thêm lần nữa.
- Phiếu đã nhập kho không được sửa hoặc hủy trực tiếp. Chỉ có thể hủy phiếu nháp; nếu nháp sai, hủy và tạo phiếu khác. Chức năng đảo giao dịch phiếu đã nhập kho hoặc trả hàng nhà cung cấp chưa được triển khai trong thay đổi này.
- `inventory_logs.receipt_detail_id` cho phép `null` đối với lịch sử cũ và có ràng buộc unique đối với các bản ghi liên kết phiếu nhập. Mã phiếu và giá nhập được truy xuất qua liên kết này; không sao chép giá nhập vào bảng lịch sử kho.
- Chức năng điều chỉnh tồn vẫn dành cho chênh lệch kiểm kê, mất hoặc hỏng hàng; giao diện phân biệt rõ điều chỉnh tồn với nhập mua hàng.
- Tổng tiền phiếu được tính từ các dòng chi tiết. Không bổ sung bảng danh mục nhà cung cấp, lịch sử giá bán, lô hàng, tính giá vốn hoặc tổng hợp theo tháng.

### API phiếu nhập

Các đường dẫn bên dưới có tiền tố `/api/goods-receipts`.

| Phương thức / đường dẫn | Quyền | Chức năng |
| --- | --- | --- |
| GET `/` | `inventory.read` | Danh sách có phân trang, tìm kiếm và lọc theo trạng thái hoặc sản phẩm |
| GET `/{id}` | `inventory.read` | Xem phiếu và các dòng chi tiết |
| POST `/` | `inventory.update` | Tạo phiếu nháp |
| POST `/{id}/post` | `inventory.update` | Xác nhận nhập kho trong một giao dịch; có thể gửi lại an toàn |
| POST `/{id}/cancel` | `inventory.update` | Hủy phiếu nháp; có thể gửi lại an toàn |

Yêu cầu tạo phiếu gồm `id`, `receiptCode`, `receivedAt` (thời gian theo định dạng ISO, múi giờ UTC), hai trường tùy chọn `supplierName`, `note` và danh sách `details: [{productId, quantity, unitCost}]`.

Chỉ tạo `id` một lần. Khi chưa xác định được kết quả yêu cầu trước, gửi lại đúng ID và nội dung đó để kiểm tra hoặc nhận lại phiếu đã tạo. Dùng lại ID với nội dung khác sẽ trả về lỗi xung đột. ID người thực hiện được lấy từ thông tin xác thực, không lấy từ nội dung yêu cầu. Người thực hiện trong lịch sử kho là người xác nhận nhập kho, có thể khác người tạo phiếu nháp.

Trên giao diện, mở **Quản lý dịch vụ → Phiếu nhập hàng** hoặc truy cập từ trang tồn kho. Lịch sử tồn kho có liên kết về phiếu nhập tương ứng.

## Chấm công nhân viên

- Đã loại bỏ entity lương, API lương, các bộ kiểm tra dữ liệu, trường lương trong DTO, quyền lương, phương thức repository, truy vấn frontend và giao diện liên quan đến lương.
- Giữ các chức năng hồ sơ nhân viên, trạng thái làm việc, ngày vào làm, quản lý ca và phân công lịch làm việc.
- Không thêm bảng chấm công: `employee_shifts` lưu một cặp giờ vào/ra cho mỗi phân công. Một nhân viên vẫn có thể được phân nhiều ca khác nhau trong ngày.
- Luồng trạng thái là `Scheduled → Present → Completed`, tương ứng đã lên lịch, đã vào ca và đã ra ca. Phân công đã hủy không được check-in.
- Nhân viên chỉ được chấm công cho phân công của mình; quản trị viên có thể chấm công cho nhân viên. Cả hai sử dụng các API check-in/check-out hiện có và thời gian do server ghi nhận.
- Check-in yêu cầu nhân viên đang làm việc, tài khoản hoạt động và ca hoạt động. Được check-in trong ngày phân công theo giờ Việt Nam; với ca qua đêm, được check-in sang ngày tiếp theo đến giờ kết thúc ca dự kiến. Chưa bổ sung ngưỡng sớm/muộn theo phút, phạt đi muộn, tính lương hoặc tự đánh dấu vắng mặt.
- Check-out yêu cầu đã check-in, không được có thời điểm sớm hơn check-in và có thể ghi nhận sau nửa đêm. Thao tác trùng gửi đồng thời sẽ trả lỗi xung đột.
- Ràng buộc CHECK trong cơ sở dữ liệu chặn giờ ra khi chưa có giờ vào hoặc giờ ra sớm hơn giờ vào. Dữ liệu thời gian cũ không hợp lệ phải được xử lý trước khi áp dụng migration.
- Quản trị viên mở **Quản lý nhân viên → Chấm công nhân viên**; nhân viên mở **Check-in / Check-out**. Giao diện hỗ trợ chọn ngày và hiển thị giờ Việt Nam.

## Kiểm thử

Unit test kiểm tra dữ liệu đầu vào phiếu nhập, quy tắc tồn kho khi tạo sản phẩm và quyền truy cập các API.

Integration test trên PostgreSQL kiểm tra giá nhập, liên kết lịch sử kho, yêu cầu gửi trùng, xác nhận đồng thời, hoàn tác khi có lỗi sản phẩm, chống tràn số lượng tồn, quyền chấm công, chấm công đồng thời, điều kiện nhân viên, ca qua đêm và ràng buộc thời gian trong cơ sở dữ liệu.

Cơ chế bảo vệ kiểm thử chỉ chấp nhận kết nối tới `localhost:5432` với tên cơ sở dữ liệu `smartpool_dev`, `smartpool_test` hoặc `smartpool_test_<32 ký tự hệ thập lục phân viết thường>`. Khi dùng cơ sở dữ liệu tạm riêng, cấu hình rõ chuỗi kết nối và đặt `SMARTPOOL_TEST_BOOTSTRAP=true` để khởi tạo vai trò kiểm thử. Không chạy test trên cơ sở dữ liệu vận hành thực tế.
