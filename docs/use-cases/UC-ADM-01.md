# UC-ADM-01 — Quản lý khách hàng

Áp dụng đặc tả 8 UC Quản lý tinh gọn đã chốt ngày 14/11/2026. BR liên quan: **07, 08, 13, 28, 29, 31, 39**. Dùng schema 30 bảng hiện hữu; không đổi bảng/cột và không sửa dữ liệu khách thật.

## Phạm vi hiện hành

| Luồng | Thực hiện |
|---|---|
| Chính 1–2 | Tìm theo mã, họ tên, tên đăng nhập, email/điện thoại; phân trang; xem hồ sơ và địa chỉ |
| Chính 3–5 | Sửa họ tên, email, điện thoại; tên đăng nhập chỉ đọc; dữ liệu trùng/sai/version cũ bị chặn |
| A1 | Chỉ khóa hoặc mở tài khoản; không yêu cầu nhập lý do, nhật ký tự ghi; khóa thu hồi phiên, mở không hồi sinh phiên cũ |
| A2 | Cấp mật khẩu tạm theo yêu cầu khách, xác nhận khớp 5–128 ký tự; thu hồi phiên, bắt khách đổi trước dùng hồ sơ/đơn |
| A3 | Thêm/sửa/chọn mặc định/ngừng địa chỉ; không nhập hoặc lưu tọa độ, không sửa snapshot địa chỉ đơn cũ |
| E1–E4 | Validation ở UI/API/SQL; rowversion; phục hồi bằng receipt sau timeout; kiểm quyền và rollback nếu audit thất bại |

Không còn tạo/xóa khách hoặc trạng thái INACTIVE tại Admin. Khách tự đăng ký trên Web. Route POST danh sách và DELETE hồ sơ đời cũ trả **410** sau kiểm quyền Admin; service/client không còn nhánh tạo/xóa. Hồ sơ INACTIVE cũ được trình bày như đã khóa và có thể mở bằng ACTIVE; không chuyển dữ liệu hàng loạt hoặc xóa lịch sử.

## File được tái sử dụng

- API: `src/Backend/FourChang.Api/Features/Admin/Customers/CustomerEndpoints.cs`, `CustomerAccountService.cs`, `CustomerAddressService.cs`.
- DTO/client: `src/Shared/FourChang.Presentation.Shared/Contracts/Admin/CustomerContracts.cs` và `ApiClients/Admin/CustomerApiClient.cs`.
- WinForms: `src/Presentation/FourChang.Desktop/Admin/Customers/Views/CustomersPage.cs`, `CustomerEditorForm.cs`, `CustomerDetailsForm.cs`, `CustomersView.cs`; tiếp tục dùng `CustomerAddressBookPanel.cs` và controller hiện hữu.
- Test: `tests/FourChang.Tests/QuanLy/UC-ADM-01/`; dùng lại xUnit/STA/Playwright và fixture hiện có, không tạo project mới.

DTO `CustomerMutation.DaXoa` chỉ giữ để đọc receipt lịch sử; mọi cập nhật mới trả false. `CustomerSaveInput.MatKhauTam` còn dùng chung validation đăng ký Web, nhưng API sửa hồ sơ không cho nhập mật khẩu qua trường này. Reset dùng `XacNhanMatKhau` rõ ràng; không lưu mật khẩu/confirmation trong audit, chỉ lưu HMAC yêu cầu.

## Kiểm thử

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-01" --logger "trx;LogFileName=adm01-simplified.trx" --results-directory artifacts/test-results
```

Các test đăng ký khách qua Web hoặc nạp dữ liệu hợp lệ vào database GUID riêng để kiểm phân trang; không mở lại chức năng tạo khách Admin. Xem `TestCases.md` trong thư mục UC để đối chiếu các luồng. Kết quả thực tế: build `FourChang.sln` đạt 0 cảnh báo/0 lỗi; 23 trường hợp riêng biệt đạt qua `adm01-simplified-final.trx` (22 đạt, một lỗi fixture phân trang) và `adm01-pagination-recheck.trx` (test đã sửa đạt). Không tính lượt lỗi ban đầu là đạt. Có test SQL/API, WinForms, Web; ảnh mới trong `artifacts/tests/xunit/UC-ADM-01-WinForms/` và `ADM01-05/`. Đã kiểm tra trực quan danh sách, form sửa, khóa và reset. Các lỗi thử nghiệm ban đầu: vượt rate limit đăng ký khi tạo dữ liệu phân trang, thiếu cột bắt buộc trong INSERT fixture, Web test chưa nhập điện thoại; đều đã sửa trong file test cũ.
