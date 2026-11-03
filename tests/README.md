# Kiểm thử C# xUnit — 4CHANG LINH NGU LAM

Toàn bộ mã thực thi kiểm thử được viết bằng **C# `.cs` và xUnit**. API dùng HttpClient; dữ liệu dùng SqlClient; Web dùng Playwright .NET; WinForms dùng STA và message loop thật. Không cần chạy file test PowerShell hoặc JavaScript.

Danh sách nghiệp vụ giữ nguyên **26 UC**; database giữ nguyên **35 bảng nghiệp vụ** đã được duyệt. Việc chuyển framework không bổ sung chức năng hoặc bảng. Các UC chưa triển khai test riêng vẫn ghi `ChuaTrienKhai` trong `test-manifest.json`.

## Chạy kiểm thử

Mở terminal tại thư mục gốc đồ án. Cần Windows, .NET SDK 8, SQL Server của dự án đang chạy trong Docker Desktop và `.env` đã cấu hình. Test tự mở API/Web riêng; không cần mở ứng dụng trước.

```powershell
cd 'D:\2.C#C++\C #\DoAnCSharp'
dotnet test FourChang.sln
```

Nếu API/Web/Desktop đang mở và build báo DLL đang bị khóa, đóng ứng dụng tương ứng rồi chạy lại. Khi đã build xong, có thể dùng `dotnet test FourChang.sln --no-build`.

| Phạm vi | Lệnh |
| --- | --- |
| Tất cả test | `dotnet test FourChang.sln` |
| Khách hàng | `dotnet test FourChang.sln --filter "Actor=KhachHang"` |
| Nhân viên | `dotnet test FourChang.sln --filter "Actor=NhanVien"` |
| Quản lý | `dotnet test FourChang.sln --filter "Actor=QuanLy"` |
| Tích hợp | `dotnet test FourChang.sln --filter "Actor=TichHop"` |
| UC-ADM-01, gồm API/Web/WinForms | `dotnet test FourChang.sln --filter "UseCase=UC-ADM-01"` |
| Chỉ API | `dotnet test FourChang.sln --filter "Layer=API"` |
| Chỉ Web | `dotnet test FourChang.sln --filter "Layer=Web"` |
| Chỉ WinForms | `dotnet test FourChang.sln --filter "Layer=WinForms"` |
| Chỉ ràng buộc/nâng cấp SQL | `dotnet test FourChang.sln --filter "Layer=Database"` |

Ghi kết quả cho giảng viên:

```powershell
dotnet test FourChang.sln --logger trx --results-directory artifacts/tests/results
```

Lệnh shell ở trên gọi `dotnet test`; không có script kiểm thử `.ps1`. Dự án vẫn cần các file cấu hình `.csproj`, `.props`, `.runsettings`, `.json` và tài liệu `.md`.

## Cấu trúc — chỉ một project kiểm thử

```text
tests/
  README.md
  test-manifest.json                    Đúng 26 UC, nguồn C# và bộ lọc chạy
  FourChang.Tests/
    FourChang.Tests.csproj              Project xUnit duy nhất; khai báo package tại đây
    xunit.runsettings
    Program.cs                         Điểm vào nội bộ cho worker WinForms
    DungChung/
      AssemblyConfiguration.cs         Tắt chạy song song để cô lập trạng thái kiểm thử
      TestApi.cs                       API và database GUID của mỗi kịch bản
      TestData.cs                      Dữ liệu và hàm đọc JSON dùng chung
      ApiResponse.cs                   Kết quả HTTP và kiểm tra status
      ChildProcess.cs                  Process con và thu thập log
      DesktopWorker.cs                 Chạy từng kịch bản WinForms từ cùng assembly
      BrowserFixture.cs                Web và Playwright .NET
      NetworkFaultProxy.cs             Mô phỏng lỗi mạng sau commit
      Mau-TestCases.md
    KhachHang/
      UC-WEB-01/RegistrationTests.cs
      UC-WEB-02/SessionTests.cs
      UC-WEB-03/ProfileTests.cs
    NhanVien/
      UC-POS-01/EmployeeIdentityTests.cs
      UC-POS-01/WinForms/EntryUiTests.cs
    QuanLy/
      UC-ADM-01/CustomerAdministrationTests.cs
      UC-ADM-01/Web/TemporaryPasswordBrowserTests.cs
      UC-ADM-01/WinForms/CustomersUiTests.cs
      UC-ADM-02/AccessAdministrationTests.cs
      UC-ADM-02/WinForms/AccessUiTests.cs
    TichHop/
      HttpRegressionTests.cs
      DatabaseTests.cs
      AccountAccess/AccountAccessTests.cs
      AccountAccess/AccountAccessBrowserTests.cs
```

Thư mục actor/UC chỉ dùng để tổ chức file, không chứa project con. Toàn bộ code được build một lần thành `FourChang.Tests.dll`.

Các Fact WinForms mở process con từ chính DLL này, chạy STA và message loop thật. Process con không phải project mới và không khai báo thêm `.csproj`. Cách này giữ `LiveSession`, URL API và cấu hình giao diện của từng UC độc lập mà không sửa code sản phẩm. Test API/Web chạy trong xUnit như bình thường.

Nếu muốn chạy trực tiếp project:

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj
```

## Quy ước trình bày mã

- Mỗi phương thức có chú thích tiếng Việt mô tả mục đích; các bước nhạy cảm giải thích thêm lý do như rollback audit, chống gửi lặp và giữ dữ liệu thật.
- Mỗi câu lệnh đặt trên dòng riêng, thụt lề bốn khoảng trắng; các nhánh và vòng lặp có block rõ ràng.
- Hạ tầng được chia theo trách nhiệm: HTTP/database, process, Web, WinForms và lỗi mạng; không đặt nghiệp vụ UC vào helper chung.
- Giữ nguyên các tình huống test hiện có, các mã TC/BR và bộ lọc Actor/UseCase/Layer. Không thay đổi 26 UC hoặc schema 35 bảng.

## Dữ liệu và bằng chứng

- Mỗi kịch bản xUnit tạo database `FourChang_Xunit_<GUID>` và chọn cổng API/Web trống. Không nhận tên database thật làm tham số test.
- Dữ liệu, quyền SQL bị cố tình thay đổi và nhật ký nằm trong database riêng; `finally` khôi phục quyền. Kết thúc test dừng đúng process đã tạo và xóa đúng database GUID đó.
- Tất cả Fact chạy lần lượt trong một assembly. Bootstrap vẫn có khóa dùng chung để tránh xung đột SQL login nếu người dùng mở hai lệnh test cùng lúc.
- Test nâng cấp dựng schema 34 bảng thật, thêm bảng hỗ trợ `sysdiagrams`, nâng cấp lên 35 và xác minh bảo toàn sơ đồ SSMS cùng 73 khóa ngoại.
- 17 nhóm ràng buộc SQL hiện có vẫn chạy bằng mã C# `DatabaseChecks` qua CLI của API; xUnit kiểm tra exit code. Không gọi PowerShell để thực thi logic SQL test.
- Playwright tự cài Chromium bằng API C# nếu thiếu. Máy cần mạng ở lần đầu; có thể đặt `FOURCHANG_TEST_CHROMIUM_PATH` để dùng trình duyệt có sẵn.
- Ảnh và log: `artifacts/tests/xunit/<suite>/<database>/`; kết quả TRX ở nơi chỉ định. Thư mục artifacts không đưa lên Git. Không chia sẻ `.env`, mật khẩu hoặc token.

## Phạm vi đã chuyển

| Bộ cũ | Mã C# thay thế | Nội dung giữ lại |
| --- | --- | --- |
| WEB-01 API | RegistrationTests.cs | Đăng ký, kiểm tra đầu vào, lặp/đụng định danh, rollback audit, quyền |
| WEB-02 API | SessionTests.cs | Đăng nhập, phiên, logout lặp, hết hạn, sai mật khẩu/khóa tạm |
| WEB-03 API | ProfileTests.cs | Hồ sơ, địa chỉ, sở hữu, version, snapshot, mật khẩu, receipt |
| POS-01 API | EmployeeIdentityTests.cs | Nhập mã/tên, trùng tên, chuẩn hóa, ngừng làm, mở/đóng ca |
| ADM-01 API | CustomerAdministrationTests.cs | Tạo/sửa/tìm/phân trang, gửi đồng thời, khóa/reset, xóa có điều kiện, lịch sử tài chính, audit |
| ADM-02 API | AccessAdministrationTests.cs | Admin duy nhất, mật khẩu, phiên, cấp/thu hồi/cấp lại máy, chốt ca, audit |
| Ba console GUI | Ba lớp *UiTests.cs | Giữ thao tác, assertion và screenshot WinForms; đổi sang Fact xUnit |
| Web mật khẩu tạm | TemporaryPasswordBrowserTests.cs | Chuyển trang bắt buộc đổi, kiểm tra lỗi, không lộ mật khẩu, CSRF, login lại |
| Web liên phân hệ | AccountAccessBrowserTests.cs | Responsive/logo, đăng ký, hồ sơ/địa chỉ, CSRF, mất phản hồi, logout offline, receipt mật khẩu tạm |
| Tích hợp API | AccountAccessTests.cs + HttpRegressionTests.cs | Đồng bộ hồ sơ, khóa/reset, quyền Customer/Staff/Admin, thu hồi máy |
| Database/nâng cấp | DatabaseTests.cs | 17 nhóm ràng buộc, 35 bảng/73 FK, nâng cấp 34→35 và bảo toàn SSMS |

Một Fact có nhiều assertion của cùng luồng nghiệp vụ. Số Fact trong báo cáo xUnit không phải số assertion hoặc số UC. Không đánh dấu UC chưa triển khai là đạt.

## Thêm test cho UC tiếp theo

Giữ mã UC đã chốt; đặt lớp `.cs` trong actor/UC sở hữu, thêm `[Fact]` hoặc `[Theory]` và `Trait` Actor/UseCase/Layer. Chuẩn bị dữ liệu bằng `TestApi.Start`, dùng `await using` để luôn dọn fixture; ghi tình huống và mã BR trong `TestCases.md`, cập nhật đúng dòng UC hiện có trong manifest. Mẫu: [Mau-TestCases.md](FourChang.Tests/DungChung/Mau-TestCases.md).
