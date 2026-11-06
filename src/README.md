# Cấu trúc src

Giữ nguyên 8 project, gom vào 3 nhóm để dễ tìm theo trách nhiệm:

```text
src/
├── Presentation/
│   ├── FourChang.Web/                    Website khách hàng
│   ├── FourChang.Admin/                  Khởi chạy Quản lý
│   ├── FourChang.Pos/                    Khởi chạy Nhân viên
│   └── FourChang.Desktop/
│       ├── Admin/
│       │   ├── Customers/Views/          Nhiều form: giữ Views và Controllers
│       │   ├── Customers/Controllers/
│       │   ├── Access/Views/
│       │   ├── Access/Controllers/
│       │   ├── Dashboard/                Các nhóm ít file đặt trực tiếp
│       │   ├── WorkSessions/             UC-ADM-13: giám sát, đối chiếu, chốt/bàn giao ca
│       │   ├── Employees/
│       │   └── Menu, Orders, Inventory…
│       ├── Staff/                        Sales, Orders, WorkSessions, Invoices
│       ├── Shared/                       Controls, Theme, Session, Security, Database, Preview
│       ├── Shell/                        Đăng nhập/mở ca và điều hướng
│       └── DesktopLauncher.cs
├── Backend/
│   ├── FourChang.Api/                    Endpoint và nghiệp vụ theo actor
│   ├── FourChang.Domain/                 35 thực thể đã chốt
│   └── FourChang.Infrastructure/         EF Core, khởi tạo và kiểm tra database
└── Shared/
    └── FourChang.Presentation.Shared/
        ├── Contracts/                    Customer, Staff, Admin, Common
        ├── ApiClients/                   Client HTTP dùng chung
        └── Preview/                      Dữ liệu/mô hình minh họa
```

## Làm việc theo phân công

- Tài, Thái: Presentation/FourChang.Web và Backend/FourChang.Api/Features/Customer.
- Phúc: Presentation/FourChang.Desktop/Staff, phần mở ca trong Shell và Backend/FourChang.Api/Features/Staff.
- Thiện: Presentation/FourChang.Desktop/Admin và Backend/FourChang.Api/Features/Admin.
- Thống nhất trước khi sửa Contracts, Domain, EF mapping hoặc các lớp Shared.

## Quy tắc tổ chức

Chức năng nhiều form dùng Views/Controllers. Chức năng ít file đặt trực tiếp trong thư mục chức năng; không tạo một thư mục Views chỉ để chứa một file. Namespace vẫn theo FourChang và nhóm nghiệp vụ; các lớp partial CafeApiClient giữ cùng namespace ApiClients. Không sửa bin/obj.

## Giao diện theo mẫu

- Web: banner ảnh cà phê thấp, thực đơn ba cột, tùy chỉnh và hình thức nhận bên phải; một cột ở điện thoại.
- POS: rail điều hướng hẹp, thực đơn/tìm kiếm bên trái và vùng đơn/thu ngân bên phải.
- Admin: thanh điều hướng nâu đậm, KPI ngang và các vùng dữ liệu cân đối.
- Theme Desktop: Presentation/FourChang.Desktop/Shared/Theme/CafeTheme.cs.
- Skin Web: Presentation/FourChang.Web/wwwroot/css/refresh.css.
- Nghiệp vụ và dữ liệu thật vẫn do API hiện có xử lý. Các UC chưa triển khai không được coi là hoàn thành chỉ vì đã có bố cục; các nút thu tiền/in của vùng POS chưa nối nghiệp vụ vẫn bị khóa.

## Chạy tại thư mục gốc đồ án

```powershell
dotnet run --project src/Backend/FourChang.Api --urls http://localhost:5181
dotnet run --project src/Presentation/FourChang.Web --urls http://localhost:5180
dotnet run --project src/Presentation/FourChang.Admin
dotnet run --project src/Presentation/FourChang.Pos
```

Các lệnh chạy từng ứng dụng ở terminal riêng; các file Chay-*.cmd cũng đã cập nhật đường dẫn. Kiểm tra: dotnet build FourChang.sln và dotnet test FourChang.sln. Không thay 26 UC hoặc schema 35 bảng khi di chuyển mã nguồn.
