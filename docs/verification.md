# Kiểm tra bản thiết kế

Ngày kiểm tra: 28/10/2026. Nền tảng: Windows, .NET SDK 8.0.425; Web chạy bằng ASP.NET Core, kiểm tra bằng Chromium headless. Đây là kiểm tra giao diện, không phải nghiệm thu chức năng nghiệp vụ.

| Hạng mục | Kết quả / phạm vi |
|---|---|
| Build solution | 5 project biên dịch thành công; 0 warning, 0 error. |
| Web route | 8/8 trang trả HTTP 200: thực đơn, đăng ký, đăng nhập, hồ sơ, giỏ, nhận món, thanh toán, đơn của tôi. |
| Desktop layout | Render 31 ảnh gồm 2 màn hình mở riêng và 17 màn hình gốc (5 POS + 11 Admin + dashboard) và 12 tab bổ sung. Không phát sinh exception khi dựng/render. |
| Web desktop | Kiểm tra ở viewport 1480×1080; 8 trang không tràn ngang. |
| Web mobile | Kiểm tra 390×844 cho thực đơn, hồ sơ, giỏ, nhận món, thanh toán và đơn cá nhân; không tràn ngang. |
| Tương tác trình bày | Kiểm tra tìm món, thông báo offline, đổi tab giao hàng/tổng mẫu, mở/đóng dialog. |
| JavaScript | Không ghi nhận page error trong lượt kiểm tra. |
| Tài liệu | Danh mục 24 UC có đối chiếu đường dẫn/view và trạng thái trong uc-screen-map.md; đặc tả gốc giữ trong references. |

Các ảnh chọn trong `previews` được lấy từ bản đang chạy/render thật. WinForms có cuộn nội dung và cuộn tab khi nội dung dài. Bảng dài dùng tooltip/scroll để đọc dữ liệu. Các tổng mẫu 84.000/99.000/70.000 là các tình huống có cấu hình khác nhau.

## Những phần chưa kiểm chứng vì nằm ngoài phạm vi UI

Chưa kiểm tra gọi API, SQL/EF, bảo mật, quyền sở hữu dữ liệu, state machine thực, công thức/ship thật, giữ chỗ/trừ kho, callback/đối soát ngân hàng, offline retry, lưu audit, in máy in, xuất Excel/PDF thật. Những nút này chỉ mở xem trước và không tạo tác động bên ngoài. Tài liệu kiến trúc mô tả điểm tích hợp cần bổ sung.

## Tự render Desktop khi review

```powershell
dotnet build MocCafe.sln
dotnet src/MocCafe.Pos/bin/Debug/net8.0-windows/MocCafe.Pos.dll --render-previews artifacts/desktop
dotnet src/MocCafe.Admin/bin/Debug/net8.0-windows/MocCafe.Admin.dll --render-previews artifacts/desktop
```

Chế độ này mở cửa sổ ngoài vùng màn hình, dựng từng view/tab và lưu ảnh, rồi thoát. Thư mục `artifacts`, `bin`, `obj` bị loại khỏi Git. Không chạy nghiệp vụ hoặc tự động thao tác ứng dụng khác.

## Kiểm tra lần chỉnh giao diện

Tên hiển thị 4CHANG LINH NGU LAM trên 8 trang Web; tất cả tiêu đề dùng font Segoe UI/fallback thống nhất. WinForms dùng Segoe UI Bold thay family Semibold không có trên máy. Hai executable render độc lập 5 UC nhân viên và 11 UC quản lý, kèm màn hình mở ca/đăng nhập riêng. File .cmd và VS Code tasks/debug được cập nhật theo project mới.

## Kiểm tra Desktop sau điều chỉnh thông báo

Giữ nguyên fixture và biểu đồ demo, chỉ bỏ các nhãn/thông báo chế độ thiết kế. Kiểm tra các màn hình/tab ở cửa sổ thường và kích thước tối thiểu; ảnh nạp từ embedded resource và header bố trí co giãn. Không thay đổi nghiệp vụ backend hoặc Web.
