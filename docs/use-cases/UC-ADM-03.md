# UC-ADM-03 — Quản lý nguyên liệu

Áp dụng đặc tả 8 UC tinh gọn; BR liên quan: 03, 08, 23, 28, 29.

- Danh sách: Nguyên liệu, Đơn vị, Ngưỡng cảnh báo, Trạng thái. Tìm theo mã/tên; chỉ Thêm/Sửa. Không có xóa, tồn an toàn, danh mục đơn vị hoặc quy đổi.
- Form: tên, đơn vị g/ml, một ngưỡng không âm (tối đa 4 số lẻ), mô tả tùy chọn và Đang sử dụng. Đơn vị có tham chiếu bị khóa cả giao diện/API. Không nhận số dư kho.
- Ngừng: đọc `/api/admin/ingredients/{id}/impact`, hiển thị các món/topping và xác nhận. Payload mang dấu phiên bản ảnh hưởng; nếu nguyên liệu hoặc công thức đã thay đổi, API từ chối và yêu cầu xem lại. Không khóa ngừng chỉ vì còn công thức.
- Catalog hiện có kiểm nguyên liệu đang dùng, nên món/topping phụ thuộc không nhận đặt mới. Kích hoạt lại chỉ khôi phục điều kiện nguyên liệu; vẫn phải đủ tồn và bật bán. Không tự đổi cờ bán, sửa công thức, xử lý đơn cũ hoặc nhập thêm kho.
- Ngưỡng chỉ để cảnh báo; khả dụng vẫn là tồn thực tế trừ giữ chỗ. Không cộng g và ml thành tổng chung.
- Giữ schema 30 bảng. TonAnToan/QuyDoi trong DTO chỉ để tương thích; khi lưu hồ sơ đặt TonAnToan=0, không xử lý giá trị này như ngưỡng thứ hai. Không cập nhật hàng loạt dữ liệu thật hay thay lịch sử/số dư.
- Mọi lệnh dùng transaction, rowversion, receipt, audit và outbox cũ. Khi chưa rõ kết quả, khóa nháp và thử lại đúng payload/mã thao tác. Các route xóa/đơn vị cũ trả 409 sau kiểm quyền Admin.

Kiểm chứng: `adm03-simplified-final.trx` 8/8 đạt; `adm03-ui-final.trx` 1/1 đạt (lặp kiểm tra UI cuối). API/SQL và WinForms chạy trên database GUID riêng. Ảnh `UC-ADM-03-desktop.png`, `UC-ADM-03-editor.png`, `ADM03-stop-confirmation.png` trong artifacts/test fixture. Không dùng database quán làm dữ liệu test.
