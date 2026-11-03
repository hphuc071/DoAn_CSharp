# Cập nhật mở ca bằng mã và họ tên

Yêu cầu ngày 02/11/2026: nhân viên nhập tên; bổ sung nhập mã nhân viên để phân biệt hồ sơ trùng tên. Thay đổi nằm trong UC-POS-01 hiện có. Giữ nguyên 26 UC, 35 bảng nghiệp vụ và cơ chế máy POS do Admin cấp quyền.

## Cách sử dụng

1. Quản lý mở **Nhân viên** để xem mã số và họ tên của hồ sơ đang hoạt động.
2. Nhân viên nhập **Mã nhân viên** và **Họ và tên** tại màn hình mở ca.
3. Hệ thống tự xác minh sau khi ngừng nhập khoảng 500ms; không có nút kiểm tra riêng.
4. Chọn ca, nhập tiền đầu ca và chọn **Mở ca làm việc**. Nút mở ca tự kiểm tra lại danh tính ngay cả khi đã xác minh tự động trước đó.

Mã nhập là `MaNhanVien` đã có trong database, ví dụ `1`. Không thêm mã hiển thị hoặc tài khoản nhân viên. Tên so sánh có dấu tiếng Việt, không phân biệt chữ hoa/thường, cho phép khoảng trắng thừa và chuẩn hóa Unicode. Hai người cùng tên được phân biệt bằng mã số. Không tìm thấy hoặc đã ngừng hoạt động thì không tạo hồ sơ hay ca mới.

## API và file liên quan

| File | Trách nhiệm |
|---|---|
| Api/Features/EmployeeIdentity.cs | Chuẩn hóa tên nhập; không bỏ dấu hoặc tự chọn hồ sơ trùng tên. |
| Api/Features/WorkEndpoints.cs | POST /api/pos/employees/verify kiểm tra mã/tên trên máy được cấp; POST /api/pos/shifts kiểm tra lại trong transaction. |
| Presentation.Shared/Integration/Contracts.cs | DTO VerifyEmployee; OpenShift bổ sung HoTen. Client phải gửi cả mã và tên khi mở ca. |
| Desktop/Shell/EntryForm.cs | Hai ô nhập mã/tên; nút kiểm tra; trạng thái xác minh; sửa thông tin thì bỏ kết quả cũ. |
| Desktop/Controls/Entry/EntryVisuals.cs | Khung input và nền thương hiệu dùng chung màn hình Nhân viên/Quản lý. |
| Desktop/DesignSystem/CafeTheme.cs và Controls/Ui.cs | Nền nâu–kem sáng, hover nhẹ, font, chiều cao bảng dùng chung. |
| Web/wwwroot/css/refinement.css | Giao diện Web thống nhất, focus rõ, tương thích điện thoại và giảm chuyển động theo cài đặt người dùng. |

POST xác minh gửi `MaNhanVien`, `HoTen`. POST mở ca gửi `MaNhanVien`, `HoTen`, `TenCa`, `MaMay`, `TienMatDauCa`. Cả hai vẫn cần cấu hình thiết bị hợp lệ qua header X-POS-Machine và X-POS-Key. Biết mã/tên không cấp quyền Admin.

## Kiểm thử đã chạy

| Bộ | Kết quả |
|---|---|
| Nhận diện nhân viên API | 21 kiểm tra đạt: trùng tên, sai mã/tên, ngừng hoạt động, không tạo ca lỗi, mở/tiếp tục/đóng ca và quyền Staff |
| Giao diện mở ca | 9 kiểm tra đạt; bao gồm nút mở ca hiển thị đầy đủ, trạng thái kiểm tra đổi theo mã, chọn đúng người trùng tên |
| AccountAccess | 25 kiểm tra API liên phân hệ và 25 kiểm tra Web đạt; có kiểm tra trang chủ không tràn ngang trên điện thoại |
| API tích hợp | 25 kiểm tra hồi quy đạt với hợp đồng mã/tên mới |
| ADM-01 | 67 API, 16 WinForms, 8 Web đạt |
| ADM-02 | 64 API, 16 WinForms đạt |

Build thành công, không lỗi/cảnh báo. Test sử dụng database tạm, không ghi cấu hình DPAPI của máy thật. Ảnh và kết quả theo từng run-id nằm trong artifacts/tests.

Lệnh riêng: `dotnet test FourChang.sln --filter "UseCase=UC-POS-01"`.

Bảng tình huống: tests/FourChang.Tests/NhanVien/UC-POS-01/TestCases-EmployeeIdentity.md. Các ca kiểm thử cũ của Admin và tích hợp đã gửi thêm HoTen khi gọi API mở ca.

Cập nhật giao diện đăng nhập: dùng logo vector thống nhất trên Web/Desktop và icon cửa sổ; bỏ icon khóa khỏi nút đăng nhập/mở ca. Tự động xác minh hủy request cũ khi mã/tên đổi và không dùng phản hồi cũ để ghi đè trạng thái hiện tại. Xem tests/FourChang.Tests/NhanVien/UC-POS-01/TestCases-AutomaticIdentity.md.
