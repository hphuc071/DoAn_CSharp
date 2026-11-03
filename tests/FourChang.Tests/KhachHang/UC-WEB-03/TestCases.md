# Bảng test case UC-WEB-03

Tiền điều kiện: Docker SQL healthy, build thành công, .env cục bộ hợp lệ. Mỗi lần chạy có database tạm 35 bảng; không dùng dữ liệu thật.

Quy tắc đối chiếu: BR-06, BR-07, BR-08, BR-13, BR-14, BR-28, BR-29.

| Mã test | Nhóm | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| TC-WEB-03-001 | Hồ sơ | Sửa tên/liên hệ rồi Admin đọc cùng khách | Web và Admin cùng dữ liệu |
| TC-WEB-03-002 | Version và dữ liệu sai | Gửi bản cũ, email khách khác, tên trống | 409/400; không ghi đè |
| TC-WEB-03-003 | Sổ địa chỉ | Thêm, sửa, chọn mặc định và bỏ địa chỉ | Một mặc định; bỏ bằng trạng thái, giữ lịch sử |
| TC-WEB-03-004 | Sở hữu | Khách B sửa địa chỉ khách A | 404; không tiết lộ hoặc sửa đối tượng ngoài quyền |
| TC-WEB-03-005 | Audit lỗi | Cấm INSERT audit rồi sửa | Rollback hồ sơ, không có receipt giả |
| TC-WEB-03-006 | Mật khẩu sai | Đổi bằng mật khẩu hiện tại sai | Từ chối, ghi sự kiện không chứa mật khẩu |
| TC-WEB-03-007 | Đổi mật khẩu | Đổi đúng rồi dùng token cũ và đăng nhập mới | Token cũ bị 401; mật khẩu mới dùng được |
| TC-WEB-03-008 | Mất phản hồi | Proxy đóng phản hồi sau PUT commit, chọn thử lại | Giữ nháp; tra receipt; không PUT lần hai |
| TC-WEB-03-009 | Receipt đổi mật khẩu | Dùng token nguồn và token khách khác tra | Nguồn đọc receipt có hạn; khách khác không đọc được |
| TC-WEB-03-010 | Antiforgery và mật khẩu HTML | POST Web thiếu token; xác nhận mật khẩu sai | Thiếu CSRF bị 400; HTML lỗi không trả mật khẩu |
| TC-WEB-03-011 | Snapshot đơn | Kiểm tra code không cập nhật DonHang khi sửa/bỏ địa chỉ | Địa chỉ và giá đã chốt của đơn giữ nguyên |

Test API nằm tại ProfileTests.cs và ProfileTests.cs. Test trình duyệt và liên phân hệ dùng tests/TichHop/AccountAccess; ảnh/result nằm trong artifacts/tests. Các bước hủy trước gửi và giữ ngữ cảnh nháp có thể kiểm tra thủ công; không đánh dấu chúng là assertion API đã chạy.
