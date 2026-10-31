# Kết quả kiểm tra tích hợp database 34 bảng

## Phạm vi

Database FourChangDb trên SQL Server Docker, EF Core 8.0.33, .NET 8. Không tạo bảng nghiệp vụ ngoài 34 bảng đã chốt và không tạo __EFMigrationsHistory. Kiểm tra tại thời điểm tích hợp; không phải chứng nhận toàn bộ use case đã hoàn thành hoặc chứng minh 3NF cho mọi phụ thuộc hàm.

| Kiểm tra | Kết quả |
|---|---|
| Build solution 8 project | PASS, 0 lỗi / 0 cảnh báo |
| Manifest và database | PASS, đúng 34 bảng, đúng tập thuộc tính, 72 FK đúng cặp cột, PK/CHECK/trigger hoạt động |
| EF Core | PASS, 34 entity, không có bảng phát sinh do mapping |
| Kiểm thử database rollback | PASS, 17 nhóm kiểm tra |
| API tích hợp | PASS, 25 kiểm tra |
| Web bằng Chromium | PASS, thực đơn SQL, lọc/tìm kiếm, đăng ký, hồ sơ, lịch sử, đăng xuất, CSRF, HttpOnly cookie và mobile |
| Desktop với API thật | PASS, 20 ảnh kiểm tra; kiểm tra trực tiếp danh sách nhân viên có dữ liệu và chọn được ca |
| Khởi tạo lặp | PASS, không xóa hoặc nạp trùng dữ liệu |
| Khởi động lại backend | PASS, dữ liệu nhân viên giữ nguyên |
| Git/secrets | PASS, .env không được theo dõi; không tìm thấy các mật khẩu/khóa thực tế trong source/config có thể đưa lên Git |
| File cũ của nhóm | README.md và schema.sql 42 bảng giữ nguyên |

## Kiểm thử database

Chạy `dotnet run --project src/FourChang.Api -- --test-db`:

- FK không tồn tại và FK ghép giữ chỗ bị từ chối.
- Chống trùng danh mục, giá âm, tồn âm, giữ chỗ vượt tồn.
- Phiên đăng nhập/công thức phải có đúng một chủ sở hữu.
- Nhật ký, phiếu đã ghi sổ và định mức đã công bố không được sửa tùy ý.
- Đơn Web bị từ chối thanh toán tiền mặt.
- Nhân viên được trùng họ tên và không có tài khoản nhân viên.
- Rowversion chặn ghi đè dữ liệu cũ.
- Backend sử dụng fourchang_app, không có quyền sysadmin/ALTER database.

Các dữ liệu trong --test-db đều rollback, không tạo thêm bảng. Identity có thể bị nhảy số sau rollback, đó là hành vi bình thường của SQL Server.

## Kiểm thử API

Chạy `pwsh -File scripts/Test-Api.ps1` (PowerShell 7), sau khi backend sẵn sàng:

- Người chưa đăng nhập không truy cập API quản lý.
- Thiết bị không có khóa không lấy danh sách nhân viên POS.
- Quản lý đăng nhập bằng tài khoản đã băm mật khẩu trong SQL.
- Thêm/sửa/ngừng làm nhân viên, từ chối phiên bản dữ liệu cũ.
- Chọn nhân viên để mở ca, tiếp tục cùng ca, yêu cầu lý do nếu chênh lệch tiền.
- Chốt ca, thu hồi quyền ca, từ chối nhân viên truy cập quản lý.
- Khách đăng ký vào bảng riêng, chỉ xem dữ liệu của mình.
- API tra cứu không trả mật khẩu/token băm.
- Đăng xuất thu hồi phiên; token cũ không dùng lại được.

API/UI tests tạo một số hồ sơ có tên ghi rõ kiểm thử. Hồ sơ nhân viên kiểm thử được ngừng làm và ca kiểm thử được chốt; không tạo giao dịch thanh toán hoặc biến động kho từ các bài thử này. Không xóa lịch sử kiểm thử bằng cách tắt FK/trigger.

## Giới hạn đã công bố

Các luồng tạo/pha/hủy đơn, thanh toán VietQR, giữ/trừ kho, ghi sổ kho, hóa đơn, chi hoàn, in/xuất và đồng bộ real-time chưa được triển khai hoàn chỉnh. Bảng/mapping/ràng buộc đã có; màn hình danh sách đọc dữ liệu thật. Không xem nút hoặc bố cục preview là nghiệp vụ đã thực hiện.

Ảnh kiểm tra được lưu trong artifacts/database-integration (bị Git bỏ qua). Hướng dẫn vận hành: HUONG-DAN-DATABASE.md.
