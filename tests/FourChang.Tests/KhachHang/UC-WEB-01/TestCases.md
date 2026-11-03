# Bảng test case UC-WEB-01

Tiền điều kiện: Docker SQL healthy, build thành công, .env cục bộ hợp lệ. Mỗi lần chạy có database tạm 35 bảng; không dùng dữ liệu thật.

Quy tắc đối chiếu: BR-06, BR-07, BR-08, BR-14, BR-29.

| Mã test | Nhóm | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| TC-WEB-01-001 | Đăng ký hợp lệ | Nhập tên, định danh, mật khẩu và xác nhận; gửi lệnh có GUID | Trả mã Customer; không có phiên; chuyển đăng nhập |
| TC-WEB-01-002 | Gửi lặp và timeout | Gửi lại cùng GUID và dữ liệu, tra receipt | Một tài khoản và một audit; cùng mã khác nội dung trả 409 |
| TC-WEB-01-003 | Dữ liệu sai | Xác nhận sai, email sai, thiếu liên hệ, GUID rỗng | API từ chối, không tạo hồ sơ dở dang |
| TC-WEB-01-004 | Định danh trùng | Đăng ký lại tên hoặc liên hệ đã dùng | Ràng buộc và service chặn trùng |
| TC-WEB-01-005 | Audit lỗi | Cấm ghi audit bằng quyền SQL trên DB tạm rồi đăng ký | Không báo thành công; tài khoản rollback |
| TC-WEB-01-006 | Phân quyền | Đăng nhập khách vừa tạo, gọi Admin và POS | Customer bị từ chối; không phát quyền nội bộ |
| TC-WEB-01-007 | Hủy đăng ký | Chọn Hủy đăng ký trước gửi | Về trang công khai; không tạo nháp server |

Test API nằm tại RegistrationTests.cs và RegistrationTests.cs. Test trình duyệt và liên phân hệ dùng tests/TichHop/AccountAccess; ảnh/result nằm trong artifacts/tests. Các bước hủy trước gửi và giữ ngữ cảnh nháp có thể kiểm tra thủ công; không đánh dấu chúng là assertion API đã chạy.
