# Database 35 bảng — 4CHANG LINH NGU LAM

## Khởi tạo trên máy mới / máy chấm

Cần .NET 8 SDK và Docker Desktop Linux containers. Mở Docker Desktop, chờ Engine chạy.

Mở PowerShell tại thư mục DoAnCSharp:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ChuanBi-Database.ps1
```

Script tự tạo mật khẩu riêng trong .env nếu chưa có; không hiển thị mật khẩu, không thay mật khẩu đang có. Nếu dùng volume SQL cũ, SQL_SA_PASSWORD phải khớp mật khẩu thực tế. Không sửa .env để đổi mật khẩu SQL của volume đã tồn tại.

Script tạo database FourChangDb nếu chưa có. Nếu gặp đúng schema 34 bảng đã xác minh, bổ sung duy nhất ThietBiPOS theo migration được chấp thuận. Schema lạ sẽ dừng, không tự xóa/sửa dữ liệu. Bộ khởi tạo không dùng EF migrations và không tạo __EFMigrationsHistory. Database có đúng 35 bảng nghiệp vụ; view, index và trigger không phải bảng mới.

## Chạy ứng dụng

- Chay-Web.cmd → http://localhost:5180.
- Chay-NhanVien.cmd → chọn hồ sơ nhân viên, chọn ca và tiền đầu ca; không đăng nhập nhân viên.
- Chay-QuanLy.cmd → tài khoản quanly; mật khẩu khởi tạo là ADMIN_INITIAL_PASSWORD trong .env. Không phải mật khẩu sa.
- Các launcher kiểm tra và khởi động backend ở http://localhost:5181. Có thể chạy Chay-Backend.cmd riêng để xem log trực tiếp.

SQL Server trên máy: 127.0.0.1,14330. Chỉ dùng sa khi quản trị/khởi tạo. Backend kết nối bằng fourchang_app với quyền SELECT/INSERT/UPDATE, chỉ DELETE tài khoản khách chưa có tham chiếu trong UC-ADM-01; các bảng khác bị DENY DELETE và không có quyền ALTER database. Mật khẩu ứng dụng và bí mật ký phiên máy chủ không nằm trong Git. Khóa riêng từng thiết bị POS được lưu bằng Windows DPAPI, không dùng khóa chung từ .env để mở POS. Chia sẻ .env.example cho giảng viên, không chia sẻ .env thật.

## Những phần đã kết nối dữ liệu thật

- Thực đơn Web/POS lấy món, cấu hình giá, công thức và tồn từ SQL Server qua API.
- Khách hàng đăng ký, đăng nhập, đăng xuất, xem hồ sơ và danh sách đơn của chính mình.
- Quản lý đăng nhập bằng mật khẩu đã băm; xem dữ liệu các phân hệ; thêm/sửa/ngừng làm hồ sơ nhân viên với kiểm soát rowversion.
- Nhân viên chọn hồ sơ, mở và kết thúc ca; thao tác lưu ca và Audit Log trong cùng transaction.
- Dashboard lấy số thu đã xác minh trừ chi hoàn thành công, không hiển thị doanh thu giả.

Các thao tác tạo/pha/hủy đơn, thanh toán VietQR, giữ/trừ kho, ghi sổ kho, phát hành hóa đơn, thực hiện hoàn tiền, in/xuất và đồng bộ real-time chưa được triển khai thành luồng ghi hoàn chỉnh. Các bảng và ràng buộc tương ứng đã có; màn hình danh sách đọc dữ liệu thật. Các màn hình giỏ hàng/checkout và chế độ --preview vẫn là bố cục xem trước, không chuyển tiền hoặc ghi đơn. Không đánh giá các nút xem trước là nghiệp vụ đã hoàn thành.

## Kiểm tra

```powershell
dotnet build FourChang.sln -m:1
dotnet run --project src/Backend/FourChang.Api -- --check-db
dotnet run --project src/Backend/FourChang.Api -- --test-db
```

--test-db kiểm tra trên SQL thật bằng các transaction rollback: FK, UNIQUE, CHECK, giá/tồn, chủ sở hữu phiên/công thức, khóa ghép giữ chỗ, lịch sử bất biến và quy tắc Web không tiền mặt. Không tạo bảng thử nghiệm và không xóa dữ liệu. Identity có thể có khoảng trống sau rollback; đó là hành vi bình thường của SQL Server.

## Cấu trúc mã nguồn

```text
src/
  FourChang.Domain/Entities/                # 35 entity, chia theo nghiệp vụ
  FourChang.Infrastructure/
    Configuration/                         # Chỉ backend đọc cấu hình SQL
    Persistence/CafeDbContext.cs
    Persistence/Configurations/            # Một cấu hình EF cho mỗi bảng
    Persistence/DatabaseBootstrap.cs        # Khởi tạo an toàn, kiểm tra manifest
    Persistence/DemoSeed.cs                 # Dữ liệu chạy thử, idempotent
    Persistence/DatabaseChecks.cs           # Kiểm tra ràng buộc, rollback
    Security/                              # Băm mật khẩu
  FourChang.Api/
    Features/                              # Auth, catalog, ca/nhân viên, tra cứu
    Security/                              # Phiên khách/QL; quyền thiết bị POS
  FourChang.Presentation.Shared/Integration/# DTO và HTTP client
  FourChang.Web/                            # Razor Pages gọi API
  FourChang.Desktop/                        # WinForms gọi API
docs/database/34-bang/
  model.json
  schema.sql
  TU-DIEN-DU-LIEU.md
  ERD.md
```

## Quy tắc dữ liệu và bảo trì

- Khách hàng và quản lý có bảng tài khoản riêng. Nhân viên chỉ có hồ sơ; không có username/password, không có MaNhanVienHienThi.
- Ca chỉ gắn MaNhanVien, không có MaTaiKhoanNhanVien. Không có bảng Quyen, TaiKhoanQuyen hoặc tài khoản STAFF.
- MaQuanLy=1 giới hạn một hồ sơ quản lý; ngừng dùng bằng trạng thái, không xóa tài khoản để tạo lại.
- CongThuc thuộc cấu hình hoặc topping (XOR). ChiTietCongThuc có khóa (MaCongThuc, MaNguyenLieu, LoaiDinhMuc): FIXED là định mức nền; SUGAR/ICE nhân phần trăm chọn/100. Định mức lưu NET; GROSS = NET/(1-TyLeHaoHut), cộng theo nguyên liệu rồi làm tròn lên 4 chữ số khi chốt/giữ kho.
- Quy đổi theo nguyên liệu cho phép đơn vị đóng gói riêng (chai/phần). Đơn vị cơ sở chỉ đổi khi chưa có phát sinh; nếu có lịch sử phải có quy trình chuyển đổi, không sửa trực tiếp.
- Danh mục/công thức hiện tại khác snapshot giao dịch. Snapshot, hóa đơn đã phát hành, phiếu đã ghi sổ và nhật ký không sửa tùy ý.
- Tổng tiền dòng/đơn và tiền thừa lấy từ view/truy vấn thay cho lưu các cột tổng có thể suy ra. TonKho là số dư vận hành cần được cập nhật cùng BienDongKho/GiuCho trong transaction khi triển khai nghiệp vụ kho.
- Lựa chọn tên nhân viên không xác minh danh tính. Máy POS dùng khóa thiết bị riêng; chỉ backend phát hành quyền ca có hạn. Không phát hành khóa thiết bị cho trình duyệt và không dùng MaCaLamViec làm token.
- Không tự động gọi Migrate/EnsureDeleted/EnsureCreated khi API khởi động. Nâng cấp schema bằng script có phiên bản và sao lưu trước, kiểm tra manifest; không thêm bảng nếu chưa có yêu cầu.
- Không ghi mật khẩu, token hay dữ liệu tài khoản nhận hoàn chưa che vào Audit Log. API tra cứu không trả các trường bí mật.

README.md của nhóm và schema.sql 42 bảng cũ được giữ nguyên. Chỉ dùng docs/database/34-bang/schema.sql cho thiết kế hiện tại. ERD 25 bảng và tài liệu cũ chỉ là lịch sử, không dùng để tạo database mới.


## UC-ADM-01 quản lý tài khoản khách hàng

Màn hình Quản lý → Khách hàng dùng API nghiệp vụ riêng cho tìm kiếm/phân trang, tạo/sửa, khóa/mở/ngừng, reset mật khẩu tạm và xóa có điều kiện. Không đổi 34 bảng/cột/FK. `fourchang_app` chỉ được DELETE `TaiKhoanKhachHang` khi không có tham chiếu; 33 bảng khác vẫn bị DENY DELETE. Chạy `ChuanBi-Database.ps1` sau cập nhật để áp dụng quyền có giới hạn. Chi tiết đối chiếu đặc tả ở `docs/use-cases/UC-ADM-01.md`.

## UC-ADM-02 — Quyền truy cập

Mở Admin → **Quyền truy cập**: tài khoản duy nhất, đổi mật khẩu, đăng xuất, cấp/thu hồi thiết bị và xử lý ca liên quan. Trên máy POS cần chạy Admin đăng ký đúng mã máy và chọn lưu cấu hình trên máy này trước khi mở ứng dụng Nhân viên. POS dùng cùng người dùng Windows để đọc được cấu hình DPAPI. Không có tài khoản cá nhân cho nhân viên.

Đổi mật khẩu Quản lý thu hồi phiên Quản lý và yêu cầu đăng nhập lại; ca POS vẫn giữ nguyên. Thu hồi thiết bị chặn lệnh mới ngay, không xóa ca hoặc giao dịch cũ. Xem [hướng dẫn UC-ADM-02](tests/QuanLy/UC-ADM-02/README.md).
