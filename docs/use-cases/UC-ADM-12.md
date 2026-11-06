# UC-ADM-12 Quản lý danh sách nhân viên

Nguồn: đặc tả 26 UC đã chốt, UC-ADM-12. Không cấp tài khoản, không có MaNhanVienHienThi, không thêm xóa hồ sơ. MaNhanVien nội bộ phân biệt người trùng tên; POS vẫn nhập mã nội bộ và họ tên theo điều chỉnh đã được chấp thuận.

## Đối chiếu đặc tả

| Nhánh | Hiện thực | Quy tắc | Kiểm thử |
|---|---|---|---|
| Chính 1 | Danh sách tên/điện thoại/ngày vào làm/trạng thái, phân trang 20 dòng | BR-08, BR-28 | EmployeeAdministrationTests; WorkforceUiTests |
| Chính 2–5 | Hồ sơ ACTIVE mới; kiểm tra trường, điện thoại và ngày; sinh khóa SQL; audit/receipt cùng transaction | BR-06, BR-14, BR-29 | Trường thiếu/sai; gửi cạnh tranh cùng mã; rollback khi DENY audit |
| A1 | Tìm tên tiếng Việt không dấu/số điện thoại, lọc trạng thái, chi tiết và rỗng hợp lệ | BR-08, BR-28 | Tìm/phân trang/rỗng và tải lại sau xóa từ khóa |
| A2 | Sửa có rowversion; không ghi lại snapshot ca hoặc giao dịch nguồn | BR-14, BR-28 | Phiên bản cũ bị từ chối |
| A3 | Ngừng/kích hoạt có lý do; còn ca hoặc đang chờ nhận bàn giao thì chặn | BR-09, BR-33 | Chặn còn ca; ngừng/kích hoạt; POS từ chối hồ sơ ngừng |
| A4 | Mở ADM-13 với MaNhanVien, đóng lịch sử quay về hồ sơ/danh sách | BR-08, BR-33 | UI bộ lọc nhân viên; API phân biệt bằng mã |
| A5 | Đóng/hủy form trước lưu không gọi API ghi | BR-05 | Kiểm tra thủ công form |
| E1–E7 | Lỗi trường rõ ràng; không UNIQUE tên/điện thoại; 401/403/404/409; không ghi offline; receipt sau timeout; rollback audit | BR-05, BR-06, BR-08, BR-14, BR-29 | Xem TestCases.md |

## Cấu trúc

- API: Features/Admin/Employees/EmployeeEndpoints.cs và EmployeeService.cs.
- Giao diện: Admin/Employees/EmployeesPage.cs; WorkforceUi.cs dùng chung hai UC.
- Hợp đồng: Contracts/Admin/EmployeeContracts.cs; ApiClients/Admin/WorkforceClient.cs.
- Chống trùng: Shared/WorkSessions/WorkforceOperation.cs dùng NhatKyHeThong hiện có.

## Hợp đồng ghi bắt buộc

POST/PUT `/api/admin/employees`: thêm `MaThaoTac` GUID khác rỗng. PUT có `PhienBanDuLieu` (base64 rowversion); đổi trạng thái qua PUT cũng phải có `LyDo`. Client mới dùng POST `/{id}/status` cho thao tác trạng thái riêng. Đây là thay đổi bắt buộc của payload ghi; các thành viên phải cập nhật Shared cùng API, không gửi payload cũ thiếu mã thao tác. GET danh sách cũ và endpoint POS giữ nguyên hình dạng.

Không tự tạo mã yêu cầu mới khi timeout. Tra GET `/api/admin/employees/operations/{MaThaoTac}`; 404 chưa có receipt tại thời điểm đọc, không chứng minh lần gửi trước chắc chắn thất bại. Nếu thử lại, giữ cùng mã và nội dung.
