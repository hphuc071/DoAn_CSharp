# Giao diện vào ứng dụng và tự xác minh nhân viên

Ngày cập nhật 02/11/2026. Giữ nguyên 26 UC và database 35 bảng. Nhân viên tiếp tục dùng máy được cấp quyền, mã nhân viên và họ tên; không có tài khoản nhân viên mới.

## Thay đổi

- Bỏ nút Kiểm tra nhân viên. Sau khi nhập đủ mã/tên và ngừng gõ khoảng 500ms, ứng dụng tự gọi API xác minh.
- Sửa mã hoặc tên sẽ hủy yêu cầu cũ và ẩn trạng thái cũ ngay. Phản hồi không còn khớp phiên nhập hiện tại sẽ bị bỏ qua. Khi đóng form, timer và request chờ được dọn.
- Bấm mở ca vẫn kiểm tra lại với API trong luồng mở ca hiện có; việc nhập tên không tự mở ca.
- Logo mới gồm bốn hạt và tách cà phê trong vòng tròn. Web dùng SVG, Desktop vẽ vector cùng hình học, có cả biểu tượng cửa sổ. Không tải font hoặc hình từ bên ngoài.
- Nút đăng nhập Khách hàng, Quản lý và mở ca chỉ hiển thị chữ. Các biểu tượng khóa của thao tác quản trị tài khoản vẫn giữ ý nghĩa nghiệp vụ riêng.
- Giữ tông nâu–kem, làm gọn màn hình đăng nhập và hình nhận diện. Web hỗ trợ điện thoại; form mới không hiện dải lỗi trống.

## Vị trí code

| File | Vai trò |
|---|---|
| src/Presentation/FourChang.Desktop/Shell/EntryForm.cs | Tự xác minh với debounce, hủy request, quản lý phiên nhập; nút vào ứng dụng không có icon khóa |
| src/Presentation/FourChang.Desktop/Controls/BrandMark.cs | Logo vector và icon cửa sổ, dọn handle GDI sau clone |
| src/Presentation/FourChang.Desktop/Shell/MainForm.cs | Logo dùng chung trên khung ứng dụng |
| src/Shared/FourChang.Presentation.Shared/Integration/CafeApiClient.cs | VerifyEmployeeAsync nhận CancellationToken để dừng request cũ |
| src/Presentation/FourChang.Web/wwwroot/assets/brand-mark.svg | Logo Web |
| src/Presentation/FourChang.Web/wwwroot/favicon.svg | Favicon cùng nhận diện |
| src/Presentation/FourChang.Web/Pages/Account/ | Nhận diện mới cho đăng nhập, đăng ký và mật khẩu tạm |
| src/Presentation/FourChang.Web/wwwroot/css/refinement.css | Bố cục và typography đăng nhập desktop/mobile |

## Test

API nhận diện nhân viên: 21 kiểm tra. WinForms: 16 kiểm tra, gồm nhập mã/tên tự xác minh, thay đổi nhanh, không có nút thủ công/icon khóa, mở đúng ca và đăng nhập Admin bằng form thật. Web: 29 kiểm tra gồm logo, không còn ảnh cốc cũ, không có thông báo trống, không tràn ngang ở mobile và các luồng tài khoản. AccountAccess có 25 kiểm tra liên phân hệ; API hồi quy có 25 kiểm tra. Các bộ sử dụng database tạm và giữ kết quả trong artifacts/tests.

```powershell
dotnet test FourChang.sln --filter "Layer=Web|Layer=WinForms"
```

Web test dùng Playwright .NET trong xUnit như tests/README.md. Test cases riêng nằm tại tests/FourChang.Tests/NhanVien/UC-POS-01/TestCases-AutomaticIdentity.md. Fixture tự chọn các cổng trống; không thay dữ liệu hay cấu hình thiết bị của database thật.
