# Database 30 bảng — 4CHANG

Cần .NET8, Docker Desktop Linux containers và cấu hình local `.env` (giữ ngoài Git).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ChuanBi-Database.ps1
dotnet run --project src/Backend/FourChang.Api -- --check-db
dotnet run --project src/Backend/FourChang.Api -- --test-db
```

Bootstrap kiểm schema, sao lưu native và xác minh checksum trước nâng cấp37→30; giữ dữ liệu đơn/tiền/ca/sơ đồ SSMS. Schema lạ sẽ dừng. Không xóa Docker volume để nâng cấp.

Web: http://localhost:15880. API: http://localhost:15881. SQL: 127.0.0.1,14330. Chạy Chay-Web.cmd, Chay-NhanVien.cmd hoặc Chay-QuanLy.cmd sau chuẩn bị database. Tài khoản quản lý là quanly, mật khẩu khởi tạo từ ADMIN_INITIAL_PASSWORD; không dùng mật khẩu sa để đăng nhập ứng dụng.

POS chọn mã/họ tên và ca, không đăng ký máy hoặc đăng nhập tài khoản nhân viên. Phiên ca do server ký vẫn bắt buộc cho bán hàng. Backend dùng fourchang_app với quyền tối thiểu; chỉ bootstrap/bảo trì dùng sa.

Chi tiết thay đổi, cột đã bỏ, danh sách bảng, backup/restore và kiểm thử: [thiết kế hiện hành](docs/database/30-bang/README.md). Các schema34/35/37 chỉ phục vụ nâng cấp/lịch sử. Không dùng EnsureCreated/EnsureDeleted hoặc EF migrations trên database này.

Các nghiệp vụ bán hàng/giữ kho/hóa đơn/hủy và hoàn tiền có kiểm transaction/audit. Thanh toán QR và giao hàng/chọn bàn hiện là mô phỏng Development; chưa kết nối ngân hàng thật. GET QR không xác nhận thanh toán.