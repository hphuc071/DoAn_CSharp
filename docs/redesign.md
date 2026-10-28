# 4CHANG LINH NGU LAM — Cập nhật thiết kế

Giữ nguyên 24 UC, màu nâu–kem và bố cục: Web có lưới món + sidebar tùy chỉnh; POS có sidebar + menu + đơn; Admin có sidebar + KPI + biểu đồ + bảng. Không thêm xử lý nghiệp vụ thật.

## Font và hình ảnh

- Dùng Segoe UI trên Windows, fallback Tahoma/Arial trên Web, mã nguồn UTF-8; bỏ font Georgia lẫn trong tiêu đề và family `Segoe UI Semibold` không có trên máy. Chữ đậm Desktop dùng `Segoe UI` với `FontStyle.Bold`.
- Các biểu tượng chính trên header Web là SVG, tránh ký tự font thay thế. CSS/JS có version hash để hạn chế trình duyệt giữ thiết kế cũ.
- Ảnh sản phẩm giữ tỷ lệ, không kéo méo theo kích thước ô; dùng lại asset hiện có.
- Bo góc nhẹ cho panel/nút, viền mảnh, thống nhất khoảng cách, cấp bậc chữ, trạng thái và màu nhấn.

## Hai ứng dụng Desktop riêng

| Ứng dụng | Project | Cách mở | Màn hình đầu |
|---|---|---|---|
| Nhân viên | `src/MocCafe.Pos` | `Chay-NhanVien.cmd` hoặc `dotnet run --project src/MocCafe.Pos` | Nhập tên mở ca; không tài khoản nhân viên. |
| Quản lý | `src/MocCafe.Admin` | `Chay-QuanLy.cmd` hoặc `dotnet run --project src/MocCafe.Admin` | Biểu mẫu đăng nhập quản lý; chưa xác thực thật. |
| Khách hàng | `src/MocCafe.Web` | `Chay-Web.cmd` hoặc `dotnet run --project src/MocCafe.Web --urls http://localhost:5180` | Website thực đơn. |

`MocCafe.Desktop` trở thành thư viện giao diện dùng chung cho hai executable. Các feature POS/Admin vẫn tách riêng; mỗi executable chỉ mở menu đúng vai trò, không có bộ chọn/chuyển vai trò trong ứng dụng. Đây là phân tách trình bày; quyền bảo mật thực thuộc giai đoạn API/xác thực sau.

Các namespace và tên solution `MocCafe` được giữ để tránh đổi tên kỹ thuật lan rộng; tên thương hiệu hiển thị là **4CHANG LINH NGU LAM**. Mở hai Desktop đồng thời được; dữ liệu mẫu không đồng bộ giữa hai cửa sổ.

## Review

Hai màn hình đầu lưu ở `previews/staff-entry.png` và `previews/admin-entry.png`; ba giao diện chính ở `web.png`, `pos.png`, `admin.png`.

Để mở thẳng giao diện nội bộ khi review: `dotnet run --project src/MocCafe.Pos -- --preview` hoặc tương tự `MocCafe.Admin`. Cờ chỉ phục vụ prototype, không phải cơ chế đăng nhập sản xuất.
