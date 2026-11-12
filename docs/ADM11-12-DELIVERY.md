# Bàn giao UC-ADM-11 và UC-ADM-12

Ngày xác minh: 12/11/2026. Nguồn: `D:/8.tailieu/dac-25-Uc.docx`, cùng các bổ sung đã được người dùng xác nhận trong chat.

## Kết quả triển khai

| UC | Chức năng |
|---|---|
| ADM-11 — Danh sách nhân viên | Thêm/xem/sửa hồ sơ và địa chỉ liên hệ, tìm tên/số liên hệ/trạng thái, phân trang, ngừng/kích hoạt có lý do, xóa chưa tham chiếu, mở lịch sử ca đúng nhân viên. Không tạo tài khoản hoặc mã hiển thị riêng. |
| ADM-12 — Ca làm việc | Lọc nhân viên/ngày/ca/trạng thái/máy lịch sử, xem CASH/CK/thu/hoàn/đơn dở/nhật ký, đối chiếu thực đếm, Admin chốt hoặc bàn giao, phân biệt khóa tạm và chờ đối chiếu, phục hồi mất phiên chỉ gửi thực đếm. |

Giữ thương hiệu **4CHANG LINH NGU LAM**, logo và palette hiện có: Coffee #805033, Dark #3A2E25, Cream #FBF8F2, Surface #FFFDF9. Dùng các control/theme chung; không thêm cơ chế Light/Dark Mode hoặc thư viện UI.

## Các quyết định đã duyệt và đã áp dụng

- Chặn ngừng/xóa người đang chờ nhận bàn giao; không âm thầm đổi người nhận hoặc tên lịch sử.
- Admin cấp mã phục hồi dùng một lần khi mất hẳn phiên; nhân viên chỉ nhập/gửi đối chiếu, không bán hoặc tự chốt bằng phiên phục hồi.
- Cấp DELETE riêng `dbo.NhanVien` cho `fourchang_app`; vẫn DENY DELETE ca, tiền và nhật ký. Kết quả kiểm trên FourChangDb: NhanVien=1, CaLamViec=0, NhatKyHeThong=0.
- Giữ 37 bảng / 74 FK, không thêm bảng/cột và không xóa dữ liệu hiện hữu. Script quyền: `database/37-bang/005-employee-delete-permission.sql`; bootstrap cho database mới cũng cập nhật đúng whitelist.

## Kiểm tra đã thực hiện

| Kiểm tra | Kết quả |
|---|---|
| Build FourChang.sln | Thành công, 0 warning / 0 error |
| Nhóm ADM-11/12 | 17 test: 15 API/SQL, 2 WinForms |
| Hồi quy danh tính POS, HTTP/phân quyền, database/nâng cấp | 4 test |
| Tổng lượt cuối `adm11-12-complete.trx` | **21 passed, 0 failed, 0 skipped** |
| Database fixture | Xác minh 37 bảng, 74 FK; kiểm tra ràng buộc và bảo toàn sysdiagrams khi nâng cấp |
| Backend local | http://localhost:15881/health/ready trả ready,37; route ca Admin không có phiên trả401 |
| Git diff --check | Không báo lỗi khoảng trắng |

Test chạy SQL thật trong database fixture tách riêng, không dùng dữ liệu quán làm fixture. WinForms chạy bằng STA worker, tương tác control/event thật và render ảnh; kiểm viewport, footer, chiều cao thẻ, luồng Admin cấp mã → POS đổi mã → gửi thực đếm → Admin chốt. Không chụp dialog có mã/token bí mật.

Đã sửa hai lỗi phát hiện từ kiểm tra UI: bộ lọc AutoSize giãn quá cao và cửa sổ chi tiết tự cuộn che phần thực đếm. Tiếp đó giảm chiều cao thẻ tiền, bỏ thanh cuộn lồng trong tab và kiểm lại cửa sổ nhỏ. Ảnh kiểm tra: `previews/adm11-12/`.

Lượt hồi quy cũ ban đầu thất bại vì còn yêu cầu máy đăng ký và coi replay cùng mã là409; đã sửa test theo phương án POS không đăng nhập và phân biệt replay200 với lệnh mới dùng version cũ409. Một lượt tiếp theo lỗi hạ tầng vì Docker tắt; sau khi khởi động SQL, toàn bộ 21 test đạt.

## Các file chính trong phạm vi này

| Nhóm | File / thư mục |
|---|---|
| Hồ sơ API | src/Backend/FourChang.Api/Features/Admin/Employees/EmployeeEndpoints.cs; EmployeeService.cs |
| Ca API | src/Backend/FourChang.Api/Features/Admin/WorkSessions/WorkSessionEndpoints.cs; WorkSessionService.cs; ShiftRecoveryService.cs |
| POS API | src/Backend/FourChang.Api/Features/Staff/WorkSessions/WorkEndpoints.cs; ReconciliationEndpoints.cs |
| Phiên / bàn giao | src/Backend/FourChang.Api/Security/ShiftSessionCodec.cs; SessionAuthentication.cs; Shared/WorkSessions/HandoverLink.cs |
| Đăng ký service | src/Backend/FourChang.Api/Program.cs |
| Quyền database | src/Backend/FourChang.Infrastructure/Database/Initialization/DatabaseBootstrap.cs; docs/database/37-bang/005-employee-delete-permission.sql |
| Contract / client | src/Shared/FourChang.Presentation.Shared/Contracts/Admin/EmployeeContracts.cs; WorkSessionContracts.cs; Contracts/Staff/WorkSessionContracts.cs; Contracts/Common/ScreenCatalog.cs; ApiClients/Admin/WorkforceClient.cs |
| Admin UI | src/Presentation/FourChang.Desktop/Admin/Employees/EmployeesPage.cs; EmployeeEditorForm.cs; Admin/WorkSessions/WorkSessionsPage.cs; WorkSessionDetailsForm.cs |
| POS UI | src/Presentation/FourChang.Desktop/Staff/WorkSessions/ShiftPage.cs; ShiftRecoveryForm.cs |
| UI dùng chung | src/Presentation/FourChang.Desktop/Shared/Controls/Admin/WorkforceDialogs.cs; Security/ShiftSessionFile.cs; Security/PosDeviceCredentials.cs; Session/LiveSession.cs |
| Điều hướng | src/Presentation/FourChang.Desktop/Shell/EntryForm.cs; MainForm.cs; Shared/Database/DatabaseViews.cs |
| Test | tests/FourChang.Tests/QuanLy/UC-ADM-11; UC-ADM-12; QuanLy/DungChung/WorkforceScenarios.cs; WorkforceUiChecks.cs; DungChung/DesktopWorker.cs; TestApi.cs; TichHop/HttpRegressionTests.cs; NhanVien/UC-POS-01/EmployeeIdentityTests.cs; tests/test-manifest.json |
| Tài liệu | docs/use-cases/UC-ADM-11.md; UC-ADM-12.md; docs/INTEGRATION-ADM-11-12.md; TestCases.md trong từng UC; .gitignore bỏ qua TestResults |

Các phương thức mới có chú thích tiếng Việt về nhiệm vụ; comment trong phần ghi giải thích version/idempotency, khóa giao dịch, lịch sử bất biến và giới hạn phiên phục hồi. Không đổi cấu trúc project, không tạo csproj mới. Repo đang có nhiều thay đổi từ các công việc trước; bảng này là phạm vi liên quan ADM-11/12, không khẳng định toàn bộ git diff thuộc đợt này.

## Cách mở và tích hợp

Chạy `Chay-QuanLy.cmd`, đăng nhập Admin rồi mở **Nhân viên** hoặc **Ca làm việc**. Cần đóng/mở lại cửa sổ Admin/POS cũ để nạp DLL mới. POS chọn gửi đối chiếu để Admin có thực đếm kiểm tra.

Xem `INTEGRATION-ADM-11-12.md` để merge Backend/Shared/Desktop/test cùng nhau và cấu hình SQL local. Chưa commit, merge hoặc push GitHub thay người dùng.

## Giới hạn kiểm chứng

- Kiểm tra tập trung ADM-11/12 và bốn test hồi quy liên quan; không tuyên bố toàn bộ test của dự án đã chạy.
- Có nhánh tương thích client thiết bị cũ để thành viên chuyển dần; luồng POS mới không bắt xác thực thiết bị. Không tự xóa bảng hay chức năng ngoài phạm vi.
- Windows DPAPI phục vụ Desktop Windows; file phiên local vẫn phải được server xác nhận. Mã/tên là nhận diện khai báo theo phương án đã chốt, không tương đương mật khẩu cá nhân.
- Các module POS khác của nhóm không được viết lại toàn bộ trong đợt này. Không thay đổi công thức thu/hoàn hoặc giao dịch nguồn để làm hết chênh lệch ca.
