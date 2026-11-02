# Kiểm thử hệ thống 4CHANG LINH NGU LAM

Thư mục chung cho đúng 26 UC đã chốt. Test thuộc actor nào đặt dưới actor đó, sau đó chia theo mã UC. Hiện đã triển khai bộ test riêng UC-ADM-01 và UC-ADM-02; các UC còn lại được ghi rõ chưa có bộ test riêng, không coi test hồi quy là đặc tả hoàn chỉnh cho các UC đó.

```text
tests/
  Run-Tests.ps1                     Điểm chạy chung, lọc actor và mã UC
  test-manifest.json                Danh sách đúng 26 UC và script hiện có
  DungChung/TestSupport.psm1         Tìm root, cấu hình, HTTP, SQL, nơi lưu kết quả
  KhachHang/                       UC-WEB-01 ... UC-WEB-08
  NhanVien/                        UC-POS-01 ... UC-POS-05
  QuanLy/
    UC-ADM-01/
      TestCases.md                 Tình huống, tiền điều kiện, bước, mong đợi, BR
      Test-Api.ps1                 67 kiểm tra API/SQL, database tạm riêng
      Test-Presentation.ps1        Điều phối WinForms và Web cùng API tạm
      WinForms/                    16 kiểm tra thao tác giao diện + ảnh
      Web/                         8 kiểm tra mật khẩu tạm + ảnh
  TichHop/
    Test-Api.ps1                  25 kiểm tra hồi quy qua nhiều actor
    Test-Database.ps1             Gọi 17 nhóm kiểm tra ràng buộc có rollback
  package.json                     Dependency Playwright dùng chung cho test Web
```

Mẫu viết test case: [DungChung/Mau-TestCases.md](DungChung/Mau-TestCases.md). Bảng đã lập cho UC-ADM-01: [QuanLy/UC-ADM-01/TestCases.md](QuanLy/UC-ADM-01/TestCases.md).

## Quy ước dùng cho mọi actor

- Tên thư mục dùng `KhachHang`, `NhanVien`, `QuanLy`, không dấu để đường dẫn dễ dùng trong script/CI.
- Mã UC giữ nguyên. Mã test theo `TC-ADM-01-API-001`, `TC-ADM-01-UI-001`, `TC-ADM-01-WEB-001`; không nhầm mã test với mã UC.
- Một file test có một chủ sở hữu UC. Test Web/WinForms hỗ trợ nghiệp vụ mật khẩu tạm do Admin cấp vẫn nằm trong UC-ADM-01; test riêng UC-WEB-02 sau này đặt trong KhachHang/UC-WEB-02.
- Test thực sự đi qua nhiều UC/actor đặt tại TichHop; hàm hạ tầng không chứa nghiệp vụ đặt tại DungChung. Không nhân đôi logic tại scripts/; các file cũ chỉ chuyển tiếp để giữ lệnh cũ hoạt động.
- Kết quả, ảnh và log tại `artifacts/tests/<Actor>/<UC>/<RunId>/`, đã bị .gitignore loại khỏi Git. Không ghi mật khẩu, token hoặc nội dung .env vào kết quả.
- Database hiện hành có 35 bảng nghiệp vụ (34 bảng gốc + ThietBiPOS đã được duyệt); database tạm có cùng schema và được xóa sau kiểm thử. `-KeepDatabase` chỉ dùng khi cần chẩn đoán, không trỏ script tạo/xóa test vào database thật.

## Chạy bộ API của UC-ADM-01

Cần .NET SDK 8, PowerShell 7, Docker Desktop/SQL Server đang chạy và .env đã chuẩn bị. Tại thư mục gốc đồ án:

```powershell
dotnet build FourChang.sln
pwsh -File tests/Run-Tests.ps1 -Actor QuanLy -UseCase UC-ADM-01
```

Runner tạo database tên `FourChang_UcAdm01_Test_<GUID>`, khởi động API ở cổng 5183 và tự dọn. Không cần dừng API thật ở 5181. Có thể đổi `-Port` nếu cần; bộ giao diện dùng thêm hai cổng kế tiếp.

## Chạy cả API, giao diện và hồi quy

Test WinForms chạy trên Windows. Test Web cần Node.js từ 20 và Playwright:

```powershell
npm --prefix tests install
cd tests
npx playwright install chromium
cd ..
pwsh -File tests/Run-Tests.ps1 -UseCase UC-ADM-01 -IncludePresentation -IncludeRegression
```

Có thể đặt `FOURCHANG_TEST_CHROMIUM_PATH` nếu dùng Chromium có sẵn. UI harness dùng vòng lặp WinForms thật, thao tác tạo/sửa/xóa và mô phỏng cắt phản hồi sau commit; không điều khiển ứng dụng đang mở của người dùng.

## Test tích hợp có sẵn

Ưu tiên `-IncludeRegression` để hồi quy chạy trên API/database tạm. Chạy trực tiếp `tests/TichHop/Test-Api.ps1 -BaseUrl ...` sẽ tạo hồ sơ kiểm thử tại database của API được chỉ định; không mặc định coi đó là chỉ đọc. Test-Database gọi bộ kiểm tra SQL dùng transaction rollback; số identity có thể có khoảng trống dù không lưu dữ liệu.

`Run-Tests.ps1 -Actor KhachHang`, `-Actor NhanVien` hoặc UC chưa có script chỉ thông báo chưa triển khai, không báo PASS giả. Khi triển khai UC tiếp theo, thêm thư mục/test case/script và cập nhật duy nhất dòng tương ứng trong test-manifest.json.


UC-ADM-02 đã có bộ test tại `QuanLy/UC-ADM-02/` (API, WinForms, đối chiếu BR). Database hiện hành: **35 bảng nghiệp vụ** sau khi bổ sung `ThietBiPOS` theo xác nhận; manifest vẫn giữ đúng **26 UC**. Xem hướng dẫn tại `QuanLy/UC-ADM-02/README.md`.
