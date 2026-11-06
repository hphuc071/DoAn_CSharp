# Chạy dự án 4CHANG LINH NGU LAM

Thư mục gốc giữ nguyên: `D:\2.C#C++\C #\DoAnCSharp`. README thành viên của nhóm được giữ nguyên.

## Tên project sau khi đổi

```text
DoAnCSharp/
  FourChang.sln
  src/
    FourChang.Web/                  # Website khách hàng
    FourChang.Pos/                  # Ứng dụng Nhân viên
    FourChang.Admin/                # Ứng dụng Quản lý
    FourChang.Desktop/              # Thư viện giao diện WinForms
    FourChang.Presentation.Shared/  # Model trình bày và dữ liệu dùng chung
  docs/database/
    THIET-KE-DATABASE-GOI-Y.md
    ERD.md
    tables.json
```

Các tên thư mục, file `.csproj`, namespace, tham chiếu, embedded resource, task/debug VS Code và script chạy được đổi đồng bộ từ `MocCafe.*` sang `FourChang.*`. Tên thương hiệu hiển thị vẫn là 4CHANG LINH NGU LAM.

## Chạy bằng terminal

```powershell
cd "D:\2.C#C++\C #\DoAnCSharp"
dotnet build FourChang.sln
```

Chọn ứng dụng cần chạy, mỗi ứng dụng dùng một terminal:

```powershell
dotnet run --project src/Presentation/FourChang.Web --urls http://localhost:15180
dotnet run --project src/Presentation/FourChang.Pos
dotnet run --project src/Presentation/FourChang.Admin
```

Hoặc mở `Chay-Web.cmd`, `Chay-NhanVien.cmd`, `Chay-QuanLy.cmd` bằng File Explorer. Shortcut `NhanVien` / `QuanLy` trỏ tới executable tên mới sau khi build. Trong VS Code vẫn dùng Run Task → Web preview / POS preview / Admin preview, hoặc F5 chọn ứng dụng.

Không dùng tên cũ `MocCafe.sln` và các lệnh `src/MocCafe.*` nữa. Giao diện, dữ liệu demo và chức năng không đổi do việc đổi tên này. Database hiện tại có đúng 34 bảng; Web và WinForms kết nối qua FourChang.Api (cổng 15181). Chạy ChuanBi-Database.ps1 trước khi mở ứng dụng. Xem [hướng dẫn database](HUONG-DAN-DATABASE.md) và [schema hiện tại](docs/database/34-bang/schema.sql). Tài liệu database cũ chỉ để tham khảo lịch sử.


## Cổng chạy local (04/11/2026)
Web: http://localhost:15180 — API: http://localhost:15181.
Máy hiện tại bị Windows dành riêng dải 5142–5241 nên 5180/5181 không bind được (socket10013).
Không sửa/xóa dải cổng hệ thống; chỉ đổi cổng ứng dụng, SQL vẫn là127.0.0.1,14330.
Launcher đọc FOURCHANG_API_URL từ biến môi trường, rồi .env, sau đó mặc định15181.
Nếu terminal cũ còn FOURCHANG_API_URL=http://localhost:5181, mở terminal mới hoặc bỏ override cũ.
Chạy Chay-QuanLy.cmd / Chay-NhanVien.cmd sẽ tự kiểm tra và mở API.
Health: http://localhost:15181/health/ready phải trả status=ready, tables=35.
Launcher phân biệt cổng không mở được, API dừng và database chưa sẵn sàng; không tự reset database.
