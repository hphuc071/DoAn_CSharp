# Docker Desktop — 4CHANG LINH NGU LAM

Docker chạy SQL Server 2022 Developer để phát triển/kiểm thử. Web và WinForms vẫn chạy trực tiếp trên Windows.

## Sử dụng hằng ngày

1. Mở Docker Desktop, chờ Engine chạy.
2. Containers → fourchang → Start. Hoặc mở Terminal tại thư mục này, chạy `docker compose up -d`.
3. Mở rộng fourchang → sqlserver-1 → Logs để xem tiến trình. Healthy nghĩa là truy vấn kiểm tra SQL đã thành công.
4. Kết thúc làm việc: Stop fourchang. Volume dữ liệu vẫn giữ nguyên.

## Kết nối từ SSMS

| Mục | Giá trị |
|---|---|
| Server | localhost,14330 |
| Authentication | SQL Server Authentication |
| Login | sa |
| Password | Giá trị SQL_SA_PASSWORD trong file .env tại thư mục này |
| Trust server certificate | Bật cho kết nối phát triển local này |

Không đưa .env lên Git. .env.example chỉ là mẫu, không chứa mật khẩu thật. Thay mật khẩu trong .env không tự đổi mật khẩu sa của dữ liệu SQL đã khởi tạo.

## Lệnh quản lý

```powershell
docker compose ps
docker compose logs --tail 50 sqlserver
docker compose stop
docker compose start
docker compose restart sqlserver
```

Dữ liệu nằm trong volume fourchang_sqlserver_data. Không xóa/Empty volume hoặc chạy docker compose down -v khi cần giữ dữ liệu. Volume không thay thế bản sao lưu database.

Thiết kế hiện tại đã chuyển sang 34 bảng. Chạy ChuanBi-Database.ps1 để tạo schema và dữ liệu mẫu an toàn; Web/WinForms kết nối qua FourChang.Api. Xem HUONG-DAN-DATABASE.md. Không chạy schema.sql 42 bảng cũ hoặc ERD 25 bảng.
