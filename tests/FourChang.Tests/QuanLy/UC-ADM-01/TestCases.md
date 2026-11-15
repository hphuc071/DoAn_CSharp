# UC-ADM-01 — Test theo đặc tả tinh gọn

BR: **07, 08, 13, 28, 29, 31, 39**. Mọi thay đổi test áp dụng ngay trong file cũ. SQL/API chạy bằng database GUID riêng, WinForms dùng worker STA, Web dùng Playwright; không tác động dữ liệu quán.

| Tình huống | Kết quả | File test hiện hữu |
|---|---|---|
| Tìm mã/tên/định danh, không kết quả, phân trang | Kết quả đúng; trang không trùng; xóa tìm kiếm/tải lại về danh sách đầy đủ | CustomerAdministrationTests.cs; WinForms/CustomersUiTests.cs |
| Sửa hồ sơ hợp lệ, tên trống, email sai, email/điện thoại trùng, đổi tên đăng nhập | Chỉ hồ sơ hợp lệ được lưu; username bất biến | CustomerAdministrationTests.cs |
| Điện thoại null/rỗng/9/10/11 số, chữ, Unicode, +84 | Chỉ đúng 10 chữ số ASCII sau trim; SQL NOT NULL/CHECK vẫn chặn | CustomerPhoneTests.cs |
| Khóa/mở không nhập lý do | Khóa thu hồi phiên, mở chỉ cho đăng nhập mới; audit tự ghi | CustomerAdministrationTests.cs; WinForms/CustomersUiTests.cs |
| Reset 4/5/129 ký tự, thiếu/sai/đúng xác nhận | Chặn sai, 5–128 khớp được cấp; không cần lý do nhập tay | CustomerAdministrationTests.cs; CustomerPasswordResetUiTests.cs |
| Reset và nhiều phiên | Thu hồi tất cả; mật khẩu cũ không dùng; tạm chỉ đổi mật khẩu; đổi xong đăng nhập đầy đủ | CustomerAdministrationTests.cs; Web/TemporaryPasswordBrowserTests.cs |
| Tạo/xóa/ngừng qua API hoặc giao diện cũ | Không có nút; API tạo/xóa 410, INACTIVE 400, dữ liệu không mất | CustomerAdministrationTests.cs; WinForms/CustomersUiTests.cs |
| Khóa khách có đơn/thu/hóa đơn/hoàn | Giữ nguyên toàn bộ lịch sử và nghĩa vụ | CustomerAdministrationTests.cs |
| Địa chỉ thêm/sửa/mặc định/ngừng; chủ sở hữu; điện thoại người nhận | Dùng chung Web/Admin, chặn sai chủ/version; không sửa snapshot đơn cũ; không tọa độ | CustomerAddressAdministrationTests.cs |
| Cùng mã đồng thời, khác nội dung, version cũ | Ghi đúng một lần; trả receipt; chặn xung đột | CustomerAdministrationTests.cs |
| Lỗi audit và mất phản hồi sau commit | Rollback khi audit lỗi; mất phản hồi giữ nháp rồi tra receipt, không PUT lần hai | CustomerAdministrationTests.cs; WinForms/CustomersUiTests.cs |
| Ẩn danh/Customer/Staff gọi API quản trị | 401/403, không đọc hoặc sửa khách ngoài quyền | CustomerAdministrationTests.cs; CustomerAddressAdministrationTests.cs |
| Dữ liệu mẫu | Seed hai lần vẫn đúng 10 khách mẫu, không xóa dữ liệu cũ | CustomerPhoneTests.cs |

Test tài khoản tích hợp được cập nhật fixture: đăng ký Web trước rồi Admin reset, thay vì gọi POST tạo khách đã bỏ. Không gỡ kiểm tra bảo mật hoặc giảm kiểm thử bằng cách bỏ test cũ; thay đúng kịch bản hết phạm vi bằng kiểm tra route bị chặn.
