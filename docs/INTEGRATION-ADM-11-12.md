# Tích hợp ADM-11 và ADM-12

## Phạm vi và cấu trúc

Theo `dac-25-Uc.docx`: ADM-11 quản lý danh sách nhân viên; ADM-12 quản lý ca làm việc. Không dùng cách đánh số của bản 26 UC trước đây.

| Lớp | Vị trí trong src | Trách nhiệm |
|---|---|---|
| API Admin | Backend/FourChang.Api/Features/Admin/Employees, WorkSessions | Hồ sơ, tìm kiếm, chốt/bàn giao, cấp mã phục hồi |
| API POS | Backend/FourChang.Api/Features/Staff/WorkSessions | Mở/khóa ca, gửi thực đếm, đổi mã phục hồi |
| Phiên và bàn giao | Backend/FourChang.Api/Security; Shared/WorkSessions | Ký/kiểm tra phiên; lưu liên kết qua FK và nhật ký hiện có |
| DTO/client | Shared/FourChang.Presentation.Shared/Contracts; ApiClients/Admin/WorkforceClient.cs | Contract dùng chung, không khai báo DTO riêng ở từng giao diện |
| Desktop Admin | Presentation/FourChang.Desktop/Admin/Employees; Admin/WorkSessions | Danh sách, biểu mẫu, đối chiếu chỉ đọc nguồn |
| Desktop POS | Presentation/FourChang.Desktop/Staff/WorkSessions | Nhân viên nhập và gửi số thực đếm; khôi phục giới hạn |
| Bảo vệ phiên local | Presentation/FourChang.Desktop/Shared/Security/ShiftSessionFile.cs | Windows DPAPI; chỉ lưu Staff/reconciliation, kiểm tra lại máy chủ |
| Test | tests/FourChang.Tests/QuanLy/UC-ADM-11, UC-ADM-12 | Chung project xUnit hiện có |

## Contract và quy tắc tích hợp

- Mọi API `/api/admin/...` yêu cầu phiên Admin. Tên/mã nhân viên không cấp quyền Admin.
- Hồ sơ dùng `MaNhanVien` nội bộ; tên có thể trùng. Không sinh tài khoản/mật khẩu nhân viên.
- Luồng mới POS không đăng nhập, không yêu cầu khóa máy. Nhập mã + họ tên đúng hồ sơ ACTIVE để mở ca; máy chủ cấp phiên ca ký. `MaMay` chỉ truy vết.
- DTO công khai nhận diện POS không trả số điện thoại/địa chỉ. Các lệnh bán hàng vẫn cần phiên hợp lệ và ca OPEN.
- Ghi hồ sơ và xử lý ca gửi `MaThaoTac` ổn định cho một nội dung; thử lại cùng mã/nội dung trả receipt. Nội dung khác phải dùng mã khác; gửi bản ghi cũ bị 409.
- Họ tên đã chốt ở lịch sử ca không đổi theo hồ sơ sửa sau đó.
- Tiền dự kiến = đầu ca + CASH đã xác minh − CASH hoàn thành công. CK tách riêng. Thiếu thực đếm là NULL, không phải 0.
- Admin không nhập thay tiền đếm. Nhân viên gửi đối chiếu trước; Admin tải snapshot mới để chốt/bàn giao.
- `MaDoiChieu` ràng buộc cả nguồn thu/hoàn và phiên bản; nguồn thay đổi dù tổng tiền bằng nhau vẫn phải tải lại.
- Bàn giao giữ khoản thu/hoàn tại ca nguồn. Ca nhận mở bởi POS, liên kết ca trước; không sửa người của ca nguồn. Người đang chờ nhận bị chặn ngừng/xóa.
- `DELIVERY_FAILED` giữ nguyên mã database, được coi là đơn kết thúc thất bại khi đối chiếu.

## Các route chính

| Method | Route | Dùng cho |
|---|---|---|
| GET | /api/admin/employees/search | search, status, page, pageSize |
| GET | /api/admin/employees/{id} | Hồ sơ và rowversion |
| POST / PUT | /api/admin/employees; /api/admin/employees/{id} | Thêm/sửa |
| POST | /api/admin/employees/{id}/status; /delete | Ngừng/kích hoạt, xóa chưa tham chiếu |
| GET | /api/admin/employees/operations/{guid} | Tra kết quả sau timeout |
| GET | /api/admin/work-sessions; /{id} | Lọc/phân trang, snapshot đối chiếu |
| POST | /api/admin/work-sessions/{id}/close; /handover; /resume; /recovery | Chốt, bàn giao, cho tiếp tục, cấp mã |
| GET | /api/admin/work-sessions/operations/{guid} | Receipt xử lý ca |
| POST | /api/pos/shifts | Mở ca; tiếp tục cần chứng minh phiên hiện hữu |
| GET | /api/pos/shifts/current/session; /reconciliation | Khôi phục ngữ cảnh và snapshot đúng ca |
| POST | /api/pos/shifts/current/reconciliation; /lock; /resume-request | Gửi đếm, khóa tạm, yêu cầu tiếp tục có phiên |
| POST | /api/pos/shifts/recovery | Đổi mã phục hồi một lần |

Tham số đầy đủ lấy từ `WorkforceClient.cs` và các record trong `Contracts/Admin/WorkSessionContracts.cs`, không tự tạo bản sao contract. API cũ có header thiết bị vẫn có nhánh tương thích để các thành viên chuyển dần; nhánh này không phải yêu cầu của luồng mới. Không xóa bảng thiết bị trong đợt này.

## Phục hồi mất phiên đã được duyệt

1. Admin mở đúng ca, kiểm tra nhân viên, nhập lý do và cấp mã.
2. Mã `PH-...` chỉ hiện ở lần cấp đầu, có hạn 15 phút, dùng một lần. Không chụp màn hình/log mã. Audit lưu hash, không lưu mã rõ.
3. Nhân viên nhập mã ca, mã nhân viên, họ tên và mã phục hồi. Phiên nhận được chỉ cho gửi đối chiếu, hạn 30 phút; không bán hàng hoặc tự chốt.
4. Admin kiểm tra số đếm và nguồn tiền rồi chốt/bàn giao.
5. Cấp lại vô hiệu mã/phiên phục hồi cũ. Nếu mất phản hồi cấp mã, tra receipt để biết kết quả; receipt không lộ mã cũ, cần cấp lại nếu chưa nhận được mã.

Khóa tạm được phân biệt với chờ đối chiếu qua nhật ký lifecycle; Admin chỉ cho tiếp tục khi có yêu cầu xác nhận hợp lệ của phiên ca, không mở lại ca đã kết thúc.

## Database và cấu hình

- Giữ 37 bảng, 74 FK; không thêm cột/bảng trong ADM-11/12. Giữ dữ liệu hiện hữu.
- Đã được duyệt cấp DELETE riêng `dbo.NhanVien`. Với database cũ, chạy `docs/database/37-bang/005-employee-delete-permission.sql` trong FourChangDb bằng quyền quản trị. Bootstrap mới cũng áp dụng whitelist này.
- Không cấp DELETE ca, tiền, nhật ký. API vẫn kiểm tra tham chiếu, phiên bản, quyền và audit nguyên tử.
- `FOURCHANG_POS_KEY` là khóa ký phía server theo tên cấu hình hiện có, không đưa vào client/Git. Không gửi khóa ký cho POS.
- `.env` nằm ngoài Git; mỗi thành viên cấu hình local theo `.env.example`. Không chia sẻ token/mật khẩu trong commit.
- Windows DPAPI chỉ giải mã được trong ngữ cảnh người dùng Windows đã lưu; file local không thay kiểm tra phiên ở API.

## Trình tự gộp code cho nhóm

1. Lưu/commit phần việc riêng trên nhánh cá nhân; tạo nhánh tích hợp từ nhánh nền của nhóm.
2. Merge phần Backend + Domain/Infrastructure + Shared contracts/client cùng Desktop và tests của thay đổi này. Không chỉ chép form.
3. Khi conflict `Program.cs`, `CafeDbContext`, `ScreenCatalog`, router Desktop: giữ cả đăng ký của thành viên khác và route mới, không chọn toàn bộ ours/theirs.
4. Chuẩn bị SQL local, chạy script quyền hẹp trên database cũ, cấu hình `.env` riêng. Dừng API cũ trước khi chạy bản build mới.
5. Chạy các lệnh dưới đây, mở Admin → Nhân viên/Ca làm việc; POS → Mở ca/Gửi đối chiếu. Sau đó kiểm tra các luồng thành viên khác trước khi merge main.

```powershell
dotnet build FourChang.sln --no-restore
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-11|UseCase=UC-ADM-12" --logger "trx;LogFileName=adm11-12.trx"
.\Chay-Backend.cmd
.\Chay-QuanLy.cmd
.\Chay-NhanVien.cmd
```

Máy mới cần `dotnet restore` trước build. Test tạo database fixture riêng và dùng SQL thật; cần Docker/SQL hoạt động, không trỏ test vào FourChangDb thật. Chưa commit/push thay người dùng.
