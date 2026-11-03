# Project kiểm thử xUnit dùng chung

Toàn bộ test API, Web, WinForms và database dùng duy nhất `FourChang.Tests.csproj`. Khai báo package/reference tại đây; các thư mục KhachHang, NhanVien, QuanLy, TichHop và DungChung không có project riêng.

Chạy từ gốc đồ án:

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj
```

Lọc UC: `dotnet test FourChang.sln --filter "UseCase=UC-ADM-01"`.

Xem [hướng dẫn đầy đủ](../README.md) về cấu trúc, dữ liệu tạm, bộ lọc và kết quả. `Program.cs` là điểm vào nội bộ của worker WinForms; không cần chạy file này trực tiếp.
