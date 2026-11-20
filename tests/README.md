# Kiểm thử 4CHANG

Project xUnit chính là tests/FourChang.Tests/FourChang.Tests.csproj. Mã test dùng chung các fixture API/SQL, Playwright và worker WinForms. Database hiện hành có 30 bảng và 64 khóa ngoại.

Danh mục tests/test-manifest.json có 21 UC gồm 8 UC khách hàng, 5 UC nhân viên và 8 UC quản lý. Trạng thái DaCoTest xác nhận có mã kiểm thử; kết quả chạy nằm trong TRX.

## Thư mục chính

~~~text
tests/FourChang.Tests/
  KhachHang/UC-WEB-01..08/     Test khách hàng; WEB07/08 có nhóm dùng chung
  NhanVien/
    UC-POS-01/               Nhận diện nhân viên, mở và kết thúc ca
    UC-POS-02/               Lập đơn bán hàng tại quầy
    UC-POS-03/               Tiếp nhận, tiến trình và danh sách đơn
    UC-POS-04/               Yêu cầu, chuyển và duyệt hủy món
    UC-POS-05/               Tra cứu hóa đơn và snapshot chứng từ
    DungChung/               Kịch bản đơn/thu tiền và UI liên quan nhiều UC
  QuanLy/UC-ADM-01..08/       Test quản lý theo 8 UC hiện hành
  QuanLy/DungChung/          Fixture và kiểm tra UI quản lý dùng chung
  TichHop/                   Liên phân hệ, schema và nâng cấp dữ liệu
  DungChung/                 API, database GUID, trình duyệt và process worker
~~~

Namespace và tên lớp được giữ để các bộ lọc FullyQualifiedName hiện có tiếp tục sử dụng. Test chung gắn UseCase theo từng phương thức; có thể mang nhiều nhãn khi đi qua nhiều UC.

## Chạy kiểm thử

Cần Windows, .NET SDK 8 và SQL Server local theo cấu hình dự án. Các test SQL tạo database GUID riêng; không chọn database quán để làm fixture.

~~~powershell
dotnet build tests/FourChang.Tests/FourChang.Tests.csproj --no-restore -v minimal
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --no-build --filter "Actor=NhanVien"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --no-build --filter "UseCase=UC-POS-03"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --no-build --filter "UseCase=UC-ADM-08"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --no-build --logger trx --results-directory artifacts/test-results
~~~

Nếu build báo DLL bị giữ, đóng process Web/API đúng repo theo hướng dẫn local. WinForms dùng worker STA; ảnh và log của fixture nằm trong artifacts/tests/xunit, kết quả TRX ở thư mục đã chỉ định.

## Harness tích hợp bổ sung

Các project FourChang.SqlVerification, FourChang.CheckoutSql, FourChang.DineInSql, FourChang.IntegrationClosure và FourChang.Br17Integration giữ những kịch bản kiểm chứng chuyên biệt. Một số chạy ngoài solution; đọc README và tham số của từng harness trước khi chạy. FourChang.Tests còn dùng FixtureSessionClock từ IntegrationClosure nên thư mục đó vẫn cần thiết.

Các schema cũ phục vụ kiểm thử nâng cấp và bootstrap nằm trong docs/database/migrations. Schema tạo mới và manifest hiện hành nằm trong docs/database/30-bang.
