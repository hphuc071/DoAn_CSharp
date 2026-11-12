# Kết quả bán hàng và thanh toán — 12/11/2026

Đã làm theo các lựa chọn được xác nhận: gửi đối chiếu quay về mở ca (không tự chốt), giỏ POS nhiều món và cấu hình từ menu chung, Web dọn form sau thêm nhưng giữ giỏ, thanh toán QR mô phỏng có nhãn riêng.

- Build toàn `FourChang.sln`: thành công, 0 warning / 0 error.
- Bộ kiểm tra liên quan: **89 passed, 0 failed, 0 skipped**, log `tests/FourChang.Tests/TestResults/pos-web-complete.trx`.
- Bao gồm API/SQL thu tiền mặt, chống lặp/đồng thời, BOM giữ chỗ, giá thay đổi, hết hạn, rollback audit, quyền ca/khách/Admin; WinForms thêm hai món và thu tiền; browser chọn món/giỏ/checkout/QR tự cập nhật; kiểm tra Production luôn chặn mô phỏng.
- Đã chạy và xem ảnh POS ở 1280×850: nút thu tiền nằm trong vùng nhìn, cấu hình và giá đọc được; kiểm tra Web đã thanh toán thành công qua xác nhận mô phỏng.
- Database giữ 37 bảng / 74 FK; không thêm cột/bảng, không reset dữ liệu.
- Có thêm QRCoder 1.6.0 để tạo QR cục bộ phía API; không gọi dịch vụ tạo QR ngoài.

Hướng dẫn thao tác, cấu hình và tích hợp: [POS-WEB-PAYMENT-FLOW.md](POS-WEB-PAYMENT-FLOW.md).
Ảnh kiểm tra không chứa QR/token bí mật: `docs/previews/pos-web-payment/`.

## Giới hạn cần biết

- Đây là thanh toán mô phỏng, không phải VietQR chuyển tiền ngân hàng thật. Khi dùng thật cần adapter QR và xác minh callback/sao kê; thay ảnh QR đơn thuần không đủ.
- QR mặc định dùng localhost, thử bằng link mở trên cùng máy. Điện thoại cần cấu hình URL/API LAN và mạng cho phép; chưa tự mở firewall hoặc public Internet.
- Giỏ POS nháp được giữ trong bộ nhớ theo phiên ca khi chuyển trang; chưa triển khai lưu giỏ POS qua việc tắt hẳn ứng dụng. Đơn đã tạo nằm trong SQL. Khi chưa rõ kết quả không tự tạo lại đơn trên thiết bị khác.
- Luồng Web vẫn giữ đặt mang đi hiện có; chưa mở chức năng giao hàng/QR bàn đang khóa.
- Bộ 89 test là phạm vi liên quan thay đổi, không khẳng định đã chạy mọi test trong solution. Chưa kiểm thử máy in vật lý.
- Chưa commit/push GitHub thay người dùng. `.env` chứa cấu hình local được giữ ngoài Git, mẫu cấu hình không có mật khẩu thật.
