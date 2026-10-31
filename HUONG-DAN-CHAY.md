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
dotnet run --project src/FourChang.Web --urls http://localhost:5180
dotnet run --project src/FourChang.Pos
dotnet run --project src/FourChang.Admin
```

Hoặc mở `Chay-Web.cmd`, `Chay-NhanVien.cmd`, `Chay-QuanLy.cmd` bằng File Explorer. Shortcut `NhanVien` / `QuanLy` trỏ tới executable tên mới sau khi build. Trong VS Code vẫn dùng Run Task → Web preview / POS preview / Admin preview, hoặc F5 chọn ứng dụng.

Không dùng tên cũ `MocCafe.sln` và các lệnh `src/MocCafe.*` nữa. Giao diện, dữ liệu demo và chức năng không đổi do việc đổi tên này. Database hiện tại có đúng 34 bảng; Web và WinForms kết nối qua FourChang.Api (cổng 5181). Chạy ChuanBi-Database.ps1 trước khi mở ứng dụng. Xem [hướng dẫn database](HUONG-DAN-DATABASE.md) và [schema hiện tại](docs/database/34-bang/schema.sql). Tài liệu database cũ chỉ để tham khảo lịch sử.
