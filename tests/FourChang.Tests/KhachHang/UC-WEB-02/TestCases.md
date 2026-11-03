# Bảng test case UC-WEB-02

Tiền điều kiện: Docker SQL healthy, build thành công, .env cục bộ hợp lệ. Mỗi lần chạy có database tạm 35 bảng; không dùng dữ liệu thật.

Quy tắc đối chiếu: BR-05, BR-06, BR-07, BR-08, BR-09, BR-29.

| Mã test | Nhóm | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| TC-WEB-02-001 | Đăng nhập và thử lại | Gửi thông tin hợp lệ cùng mã hai lần | Cùng token, một phiên; audit thành công |
| TC-WEB-02-002 | Đăng xuất và thử lại | Logout rồi gọi lại và dùng token cũ | Logout an toàn; token không mở hồ sơ; một audit |
| TC-WEB-02-003 | Hết hạn | Điều chỉnh thời gian phiên trong DB tạm, giữ CHECK hợp lệ | API trả 401 |
| TC-WEB-02-004 | Audit lỗi | Cấm ghi audit rồi đăng nhập | Phiên mới rollback |
| TC-WEB-02-005 | Thử sai | Gửi cùng lần sai hai lần; sau đó đủ năm mã sai riêng | Không tăng bộ đếm do lặp; đủ năm lần khóa tạm |
| TC-WEB-02-006 | Admin khóa/reset | Chạy bộ AccountAccess | Khóa/reset chặn token cũ; mở lại không hồi sinh token |
| TC-WEB-02-007 | Mật khẩu tạm | Login sau reset, gọi hồ sơ, đổi mật khẩu | Phiên hạn chế bị 403; đổi xong thu hồi; đăng nhập mới |
| TC-WEB-02-008 | Logout offline | Proxy ngắt API khi logout rồi nối lại | Cookie đăng nhập bị xóa ngay; cookie chờ mã hóa; server thu hồi khi kết nối lại |
| TC-WEB-02-009 | Giỏ/bàn nháp | Đăng nhập/đăng xuất khi có ngữ cảnh nháp | Luồng tài khoản không xóa giỏ/bàn hoặc coi nháp là đơn đã gửi |

Test API nằm tại SessionTests.cs và SessionTests.cs. Test trình duyệt và liên phân hệ dùng tests/TichHop/AccountAccess; ảnh/result nằm trong artifacts/tests. Các bước hủy trước gửi và giữ ngữ cảnh nháp có thể kiểm tra thủ công; không đánh dấu chúng là assertion API đã chạy.
