# Kiểm tra responsive frontend

Chạy Vite ở một terminal:

```powershell
npm run dev -- --host 127.0.0.1 --port 5188 --strictPort
```

Trong terminal khác, chạy bộ kiểm tra bằng Node và Playwright. Nếu Playwright
đã có trong `node_modules`, chỉ cần chạy lệnh cuối. Nếu dùng runtime có sẵn
của Codex Desktop, đặt `PLAYWRIGHT_MODULE_PATH` thành đường dẫn package
`playwright` mà `load_workspace_dependencies` trả về:

```powershell
$env:PLAYWRIGHT_MODULE_PATH = '<đường dẫn tuyệt đối đến package playwright>'
node tests/responsive.browser.mjs
```

Mặc định dùng Microsoft Edge đã cài đặt, kiểm tra 39 URL ở các độ rộng
320, 375, 390, 600, 768, 1024 và 1440 px. Bộ kiểm tra còn mở menu khách hàng,
menu quản trị, bảy form tạo mới, popup ngày/giờ, chọn phương thức thanh toán POS,
tab email, giỏ hàng, thanh toán online, menu khách chưa đăng nhập và màn hình chấm công của nhân viên. Nó phát hiện phần tử
tràn khỏi viewport, popup bị cắt theo chiều dọc, modal trong suốt, trang lỗi khi render và lỗi JavaScript.

Toàn bộ API được giả lập, không gọi backend hoặc tạo dữ liệu thật. Các màn hình
sử dụng cả danh sách trống và dữ liệu mẫu có sẵn; đây là kiểm tra bố cục, không
thay thế kiểm thử nghiệp vụ hay kiểm thử trên thiết bị thật.

Ảnh chụp và `results.json` được lưu vào thư mục `smartpool-responsive` trong
thư mục tạm của hệ điều hành. Nếu kiểm tra bị gián đoạn, xem `failure.png`.

Các biến tùy chọn:

- `RESPONSIVE_BASE_URL`: URL Vite, mặc định `http://127.0.0.1:5188`.
- `RESPONSIVE_BROWSER`: browser channel Playwright, mặc định `msedge`.
- `RESPONSIVE_WIDTHS`: danh sách độ rộng, ví dụ `320,768`.
- `RESPONSIVE_HEIGHT`: chiều cao viewport, mặc định 900 px; có thể dùng 390 px để kiểm tra điện thoại nằm ngang.
- `RESPONSIVE_ROUTES`: danh sách URL cần chạy riêng, phân cách bằng dấu phẩy.
- `RESPONSIVE_SKIP_INTERACTIONS=1`: chỉ kiểm tra các trang.
- `RESPONSIVE_OUTPUT_DIR`: thư mục lưu kết quả và ảnh.
