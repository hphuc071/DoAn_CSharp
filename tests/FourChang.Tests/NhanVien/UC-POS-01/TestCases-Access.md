# POS-01 phần quyền truy cập liên quan ADM-02

| Tình huống | Mong đợi |
|---|---|
| Máy được Admin cấp cấu hình | Đọc được nhân viên và mở ca, không tạo tài khoản Staff |
| Token ca gọi hồ sơ Customer | 403 |
| Token ca gọi quản lý khách hoặc cấp máy | 403 |
| Admin thu hồi máy đang có ca | Token ca bị 401 ngay |
| Kiểm tra ca sau thu hồi | Giữ bản ghi, trạng thái và lịch sử, không tự đóng ca |

Chạy EmployeeIdentityTests.cs để gọi bộ tích hợp AccountAccess trên database tạm. Đây là phần quyền; chưa thay thế test toàn bộ POS-01. Bộ ADM-02 hiện có còn kiểm tra cấp lại, version và xử lý ca liên quan.
