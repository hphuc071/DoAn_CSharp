# Chuẩn hóa font và tinh chỉnh giao diện

## Phạm vi và kết quả

Giữ tên **4CHANG LINH NGU LAM**, logo, bố cục chính, luồng nghiệp vụ, API và database. Thay đổi lần này nằm ở lớp trình bày và launcher POS; không thay dữ liệu quán.

- Quét source: không tìm thấy file sai UTF-8 hoặc chuỗi lỗi mã hóa nghi vấn. Kiểm tra tên trong database bằng so sánh binary cũng không tìm thấy chuỗi nghi vấn. Vì vậy không tự sửa tên/dữ liệu.
- Font ưu tiên Segoe UI, dự phòng Tahoma/Arial có sẵn trên Windows. Không tải font mạng.
- Phát hiện bootstrap chưa gọi `CafeVisuals.Initialize()`: nối lại để modal/control mới cũng nhận style chung.
- Nhãn dùng renderer Unicode native, không hiểu dấu `&` trong dữ liệu là mnemonic. Hàng cố định thiếu chiều cao được nới theo font và phần dấu.
- Button một dòng có ellipsis thay vì xuống dòng rồi bị cắt trong vùng nút cố định.
- ComboBox đo chiều cao chữ thực tế, cập nhật khi font/DPI thay đổi. Các input frame căn lại khi font thay đổi.
- Một số dialog chuyển sang DPI scaling để tránh vừa scale theo font vừa dùng kích thước thiết kế cố định.
- Dòng thương hiệu phụ được hạ nhẹ để không chồng chữ. Tên người làm trên header được đo chiều rộng thay vì cố định 160px.
- Launcher nhân viên build lại khi có SDK, tránh mở DLL cũ sau khi sửa giao diện.

## Màu và typography Web

| Thành phần | Tinh chỉnh |
|---|---|
| Nền | `#f3eee3` → `#f8f4ed`, kem sáng nhẹ |
| Surface | `#fffcf7` → `#fffdf9` |
| Border | `#dfd6c9` → `#e4d9cc` |
| Chữ phụ | `#796f63` → `#716252`, tăng độ rõ |
| Chủ đạo | Giữ nâu `#75452d` |
| Nội dung | 15px, line-height 1.6 |
| Ô nhập biểu mẫu | 16px, cao tối thiểu 44px |
| Tiêu đề | Bỏ ép khoảng cách chữ âm, line-height 1.35–1.4 |
| Nút | Căn giữa icon/chữ, gap 8px, cao tối thiểu 44px |

Giữ các hover/pressed transition hiện có. Thêm chuyển border/focus 180ms cho ô nhập, giữ `prefers-reduced-motion`. Không có chức năng chuyển Light/Dark được thêm lại. Mobile hiển thị lại hồ sơ và đăng nhập/đăng xuất, thay vì bị CSS cũ ẩn mất.

## Các file của lần sửa font này

Đường dẫn dưới đây tính từ gốc repo.

- `Chay-NhanVien.cmd`
- `src/Presentation/FourChang.Desktop/DesktopLauncher.cs`
- `src/Presentation/FourChang.Desktop/Shared/Theme/CafeTypography.cs` (mới)
- `src/Presentation/FourChang.Desktop/Shared/Theme/CafeTheme.cs`
- `src/Presentation/FourChang.Desktop/Shared/Theme/CafeVisuals.cs`
- `src/Presentation/FourChang.Desktop/Shared/Controls/Entry/EntryVisuals.cs`
- `src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminListLayout.cs`
- `src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminUi.cs`
- `src/Presentation/FourChang.Desktop/Shell/MainForm.cs`
- `src/Presentation/FourChang.Desktop/Admin/Customers/Views/CustomerEditorForm.cs`
- `src/Presentation/FourChang.Desktop/Admin/Customers/Views/CustomerDetailsForm.cs`
- `src/Presentation/FourChang.Desktop/Admin/Ingredients/IngredientEditorForm.cs`
- `src/Presentation/FourChang.Desktop/Admin/Recipes/RecipeEditorForm.cs`
- `src/Presentation/FourChang.Desktop/Admin/Menu/MenuEditorForm.cs`
- `src/Presentation/FourChang.Desktop/Admin/Access/Views/AdminPasswordForm.cs`
- `src/Presentation/FourChang.Desktop/Admin/Access/Views/PosDeviceForm.cs`
- `src/Presentation/FourChang.Web/wwwroot/css/refresh.css`
- `tests/FourChang.Tests/Program.cs`
- `tests/FourChang.Tests/QuanLy/DungChung/AdminFeaturesUiTests.cs`
- `tests/FourChang.Tests/TichHop/TypographyUiTests.cs` (mới)
- `tests/FourChang.Tests/TichHop/TypographyBrowserTests.cs` (mới)

Các diff nghiệp vụ có sẵn trong working tree thuộc các công việc trước; không phải thay đổi nghiệp vụ của lần sửa font.

## Kiểm chứng

- Build `FourChang.sln`: thành công, 0 warning, 0 error.
- `typography-final.trx`: **29/29 test đạt**. Gồm font/layout Web và Desktop, Admin 01–05/dashboard, reset mật khẩu qua UI, đổi mật khẩu tạm trên Web, số điện thoại, nguyên liệu, xác định nhân viên/mở ca.
- Sau chỉnh DPI dialog thực đơn: `typography-dialog-final.trx` kiểm tra lại 6/6 màn hình Admin đạt (các test lặp xác nhận, không cộng thành số test riêng).
- Web: chụp ở 1440px và 390px; thực đơn, đăng nhập, đăng ký, giỏ hàng; truy cập hồ sơ/đơn/checkout bằng phiên khách thật; kiểm tra UTF-8, font computed, không tràn ngang và media giảm chuyển động. Không khẳng định đã kiểm tra mọi trạng thái thanh toán khi không có đơn tương ứng.
- Desktop: chụp entry Admin/POS; dashboard, khách, nhân viên, thực đơn, nguyên liệu, BOM, tồn kho, chứng từ, đơn, hóa đơn, hoàn tiền, báo cáo; 5 route POS; kích thước cửa sổ thường và compact. Xem ảnh tổng quan và ảnh gốc các màn hình chính/dialog nguyên liệu/BOM.
- Các trang còn đang là bảng chỉ đọc được giữ nguyên khả năng hiện tại. Việc chụp trang không chứng minh UC đó đã triển khai đầy đủ nghiệp vụ.
- Kiểm tra trên DPI hiện tại của máy; chưa thay DPI hệ điều hành để xác nhận trực tiếp tất cả mức 125–200%.
- Test tạo database fixture riêng và dọn sau chạy; không xóa hay thay dữ liệu thật.

## Xem bản mới

Đóng ứng dụng Admin/POS cũ rồi mở lại `Chay-QuanLy.cmd` / `Chay-NhanVien.cmd`. Với Web đang chạy trước lần build, khởi động lại `Chay-Web.cmd`, sau đó Ctrl+F5. Không cần sửa database để dùng giao diện này.

Đã chỉnh local; chưa commit hoặc push GitHub.
