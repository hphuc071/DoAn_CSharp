# Test cases UC-ADM-02

Phạm vi giữ đúng UC-ADM-02 và BR-05, BR-06, BR-07, BR-08, BR-09, BR-14, BR-29 trong tài liệu 26 UC. Không thêm tài khoản nhân viên hoặc Admin thứ hai. Chạy trên database tạm, không sửa mật khẩu/dữ liệu kinh doanh thật.

| Nhóm | Luồng / quy tắc đối chiếu | Ca kiểm thử và kết quả mong đợi |
|---|---|---|
| Đăng nhập | Chính; E1; BR-06/07/08 | Ẩn danh, sai mật khẩu, khóa có thời hạn bị từ chối; Admin duy nhất đăng nhập đúng được cấp phiên |
| Phạm vi quyền | Chính; E2/E5; BR-06/09 | Customer/Staff không gọi API Admin; không có API tạo Admin thứ hai; ma trận giao diện chỉ đọc |
| Mật khẩu tạm | A1; BR-07/08 | Phiên giới hạn không vào tài khoản/thiết bị Dashboard; đổi đúng mới cho phép đăng nhập đầy đủ |
| Đổi mật khẩu | A2; E4; BR-07/08/29 | Kiểm tra mật khẩu hiện tại, xác nhận, độ dài, rowversion; đổi thành công thu hồi tất cả phiên Admin, giữ ca POS |
| Đăng xuất | A1; BR-08 | Thu hồi phiên Admin, không đóng hoặc xóa ca đang mở |
| Đăng ký máy | A3; BR-06/09/14 | Mã máy duy nhất, khóa riêng, lý do bắt buộc; header máy/khóa phải khớp; chỉ trả DTO an toàn |
| Thu hồi/cấp lại | A3; E3/E4/E5 | Thu hồi chặn ngay khóa/phiên cũ; giữ ca và lịch sử; chưa xử lý ca thì không cấp lại; cấp lại đổi khóa không làm sống lại khóa cũ |
| Ca liên quan | A3/E3; BR-09/14 | Có đơn dang dở thì không chốt; đủ điều kiện lưu tiền thực đếm, người duyệt, lý do và giữ chủ ca |
| Gửi lặp / mất phản hồi | E6; BR-05/29 | Cùng mã và nội dung trả kết quả cũ; đổi nội dung bị từ chối; tra đổi mật khẩu bằng phiên nguồn đã thu hồi chỉ trong cửa sổ cho phép |
| Giao dịch và audit | E6; BR-14/29 | Cấm INSERT audit để mô phỏng lỗi: thu hồi thiết bị/đổi mật khẩu quay lui cả dữ liệu và phiên; không có receipt thành công giả |
| Bí mật | BR-07/09/14 | Audit không chứa mật khẩu, khóa hoặc hash xác thực; DPAPI roundtrip đúng, file không chứa khóa rõ |
| WinForms | Chính/A2/A3 | Ba tab, dữ liệu thật, che khóa/mật khẩu, cấp/thu hồi thực sự qua form; xác nhận sai giữ form; đổi đúng buộc đăng nhập lại |
| Hồi quy | BR-05/06/08/09 | Chạy API tích hợp, UC-ADM-01 và bộ ràng buộc DB; bảo toàn chức năng đã có |

Chi tiết từng assertion có tên tiếng Việt trong `Test-Api.ps1` và `WinForms/Program.cs`; số lượng PASS thực tế ghi trong `result.json`, không đánh dấu các UC chưa triển khai là PASS.
