# Bản đồ giao diện giỏ hàng, thanh toán, đơn và hóa đơn

Cập nhật 12/11/2026. Mục đích: đọc bản đồ này trước, sau đó chỉ mở đoạn cần sửa.

| Khu vực | File chính |
|---|---|
| Web dùng chung | Pages/Shared/_Layout.cshtml; wwwroot/css/commerce.css |
| Giỏ hàng | Pages/Cart/Index.cshtml; wwwroot/js/cart-page.js |
| Checkout | Pages/Checkout/Index.cshtml |
| Trang thanh toán | Pages/Checkout/Payment.cshtml |
| Đơn và hóa đơn khách | Pages/Orders/Index.cshtml; Details.cshtml; Services/CommerceLabels.cs |
| Đơn/hóa đơn Admin | Admin/Orders/OrdersManagementPage.cs; Admin/Invoices/InvoicesManagementPage.cs |
| Style Desktop chung | Shared/Controls/Admin/CommerceWorkspaceVisuals.cs |
| Đơn/hóa đơn nhân viên | Staff/Shared/StaffDocumentsPage.cs; Shared/Database/DatabaseViews.cs |
| Giỏ/thu ngân nhân viên | Staff/Sales/PosSalesPage.cs |

Các đường dẫn Web tính từ src/Presentation/FourChang.Web, Desktop từ src/Presentation/FourChang.Desktop.

## Đã chỉnh

- Giỏ: món/cấu hình/giá và nhóm nút rõ ràng, tóm tắt tiền riêng.
- Checkout: phân cấp thông tin, các bước đặt món, form và tổng thanh toán đồng bộ.
- Thanh toán: QR/kết quả bên trái, tóm tắt tiền bên phải, thông tin đối soát thu gọn.
- Chi tiết đơn: mốc đã ghi nhận, món, tổng tiền, hoàn và thẻ hóa đơn riêng.
- Desktop Admin: bảng và toolbar thống nhất, phân trang xuống cuối; chi tiết hóa đơn có số tiền chứng từ/thu/hoàn riêng.
- Desktop nhân viên: danh sách đơn/hóa đơn thay bảng thô, tìm trong dữ liệu đã tải, xem chi tiết chỉ đọc. Giữ giới hạn API hiện có tối đa100 bản ghi; không giả có tìm toàn database.
- Giữ 4CHANG LINH NGU LAM, logo, màu nâu/kem; không sửa backend/database/API/quy tắc tiền hoặc mở khóa chức năng đang khóa. Các ID JavaScript, route và handler cũ được giữ.

## Kiểm tra

Build FourChang.sln: 0 warning / 0 error.
11 test liên quan đã đạt sau kiểm tra lại (9 đạt lượt đầu, 2 Web gián đoạn SQL đạt khi chạy lại; lượt cuối3 UI đạt sau sửa căn lề).
Log: commerce-ui-round1.trx, commerce-web-review.trx, commerce-ui-final.trx trong tests/FourChang.Tests/TestResults.

Test mới: KhachHang/CommercePresentationBrowserTests.cs (1440px/390px, 5 trang), NhanVien/StaffDocumentsUiTests.cs (2 route, xem/tìm/resize/dialog), QuanLy/DungChung/StaffDocumentsUiChecks.cs. Dùng chung csproj xUnit hiện có, SQL fixture riêng.
Chạy tập trung: dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "Feature=CommercePresentation"

Chưa kiểm tra máy in vật lý hoặc mọi test của solution. Giữ nguyên việc tải PDF phụ thuộc nguồn tài liệu đã lưu và khóa hủy đơn hiện có. Không thêm nút xử lý giả cho nhân viên.
## Tinh chỉnh đơn hàng theo phản hồi
- Admin: tối đa ba nút chính; thao tác kế tiếp theo trạng thái, các thao tác còn lại trong menu. Giữ nguyên lệnh API, kiểm tra và hộp xác nhận.
- Web: căn trái nội dung món, tăng chữ, thu gọn thẻ hóa đơn/tổng tiền; tiến độ món, đối soát, hoàn tiền và hỗ trợ hủy có thể mở rộng khi cần.
- Build test project: 0 warning, 0 error. Hai test tập trung đạt (compact-orders-final.trx): thanh công cụ WinForms và 5 trang Web ở 1440/390px. Đã xem ảnh thực tế Desktop và chi tiết đơn Web.
