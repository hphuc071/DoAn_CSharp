# Liên kết tài khoản khách hàng và quyền truy cập

Phạm vi được yêu cầu: WEB-01/02/03 liên kết ADM-01; quyền Customer và phiên POS-01 liên kết ADM-02. Không thêm UC, không thay đổi 35 bảng hiện hành. Nhóm phụ trách: Tài và Thái — Web; Phúc — POS; Thiện — Admin.

## File và trách nhiệm

| File hoặc thư mục | Làm gì và vì sao |
|---|---|
| Api/Features/Customers/CustomerAccessService.cs | Đăng ký, đăng nhập, đăng xuất. Cùng bảng Customer với Admin; audit và phiên commit nguyên tử. |
| Api/Features/Customers/CustomerSelfService.cs | Hồ sơ và địa chỉ của khách hiện tại, đổi mật khẩu. Tự lấy chủ sở hữu từ token; version chống ghi đè. |
| Api/Features/Customers/CustomerSelfEndpoints.cs | Định tuyến và bắt quyền Customer; không nhận mã chủ tài khoản từ trình duyệt. |
| Api/Features/Customers/CustomerAccountService.cs | Phần ADM-01 có sẵn: Admin khóa, mở, reset và quản lý hồ sơ. |
| Api/Features/Access/ | Phần ADM-02 có sẵn: Quản lý duy nhất và cấp/thu hồi máy POS. |
| Api/Security/SessionAuthentication.cs | Mỗi yêu cầu đều kiểm tra tài khoản, phiên, ca và trạng thái máy; giao diện không thể tự cấp quyền. |
| Presentation.Shared/Integration/Customers/CustomerSelfContracts.cs | DTO của hồ sơ, địa chỉ, kết quả thao tác; không có mật khẩu băm hay token trong receipt. |
| Presentation.Shared/Integration/Customers/CustomerSelfApiClient.cs | Các lời gọi API dùng chung cho Razor Pages. |
| Web/Pages/Account/ | Đăng ký, đăng nhập, hồ sơ, địa chỉ, mật khẩu, đăng xuất. |
| Web/Security/PendingCustomerLogout.cs | Xóa xác thực cục bộ khi API mất mạng; token chờ thu hồi nằm trong cookie DataProtection HttpOnly. |
| Web/wwwroot/js/account.js | Cảnh báo nháp chưa lưu, chặn submit khi trình duyệt offline và xác nhận bỏ địa chỉ. |
| tests/FourChang.Tests/KhachHang/UC-WEB-01 đến 03/ | Test API và bảng test case riêng theo UC. |
| tests/FourChang.Tests/TichHop/AccountAccess/ | Khóa/reset xuyên phân hệ; quyền POS; test Web có mô phỏng mất phản hồi và offline. |

## Đối chiếu đặc tả và quy tắc

| UC | Luồng thực hiện | BR liên quan |
|---|---|---|
| WEB-01 | Định danh hợp lệ, chuẩn hóa, kiểm tra trùng; xác nhận mật khẩu phía API; transaction cùng audit, mã thao tác; đăng ký trả mã khách và chuyển Login, không tự cấp phiên. | BR-06, 07, 08, 14, 29 |
| WEB-02 | Phiên Customer có hạn; thử sai và khóa tạm; mật khẩu tạm chỉ có quyền đổi; audit cùng phiên; logout idempotent; xóa cookie và chờ thu hồi lại khi mất mạng. | BR-05, 06, 07, 08, 09, 29 |
| WEB-03 | Sửa tên/liên hệ; thêm, sửa, bỏ địa chỉ; một địa chỉ mặc định; kiểm tra sở hữu và version; đổi mật khẩu thu hồi phiên; giữ snapshot đơn cũ. | BR-06, 07, 08, 13, 14, 28, 29 |
| ADM-01 | Web và Admin đọc cùng hồ sơ; khóa/reset thu hồi phiên; mở lại không hồi sinh token cũ; mật khẩu tạm yêu cầu đổi trước hồ sơ. | Theo đặc tả ADM-01 hiện có |
| ADM-02 và POS-01 | Máy được cấp mới lấy nhân viên và mở ca; Customer/Staff không gọi Admin; máy thu hồi chặn token ca, giữ nguyên lịch sử ca. | Theo đặc tả ADM-02 và phạm vi quyền POS-01 |

## Hợp đồng xử lý lại

Giữ nguyên MaThaoTac khi chưa rõ kết quả. Trước gửi lại, Web tra receipt của mã đó. Lệnh khác nội dung dùng cùng mã bị từ chối. Receipt không lưu mật khẩu/token. Đăng nhập có mã thao tác để việc gửi lặp không cấp thêm phiên hoặc tăng bộ đếm sai lần hai.

Mọi thao tác sửa hồ sơ dùng PhienBanDuLieu; địa chỉ dùng cả version hồ sơ và địa chỉ. Khi xung đột, khách tải lại và đối chiếu, không tự ghi đè. Bỏ địa chỉ dùng DangSuDung=false có sẵn để giữ lịch sử và phù hợp quyền database hiện hành.

Sau đổi mật khẩu, token nguồn đã thu hồi chỉ đọc receipt của đúng lệnh đó trong 15 phút và khi phiên chưa hết hạn; không dùng lại để mở hồ sơ. Đăng xuất offline chỉ có thể thu hồi phía server khi có request tiếp theo và API kết nối lại; không báo đã thu hồi server khi còn mất mạng.

## Kiểm thử

Build solution, mở Docker Desktop rồi chạy từ thư mục gốc:

```powershell
dotnet test FourChang.sln --filter "UseCase=AccountAccess"
```

Web test dùng Playwright .NET trực tiếp trong C#. Mỗi Fact tạo database GUID, chọn cổng API/Web/proxy trống, tự dọn bằng await using/finally. Kết quả dưới artifacts/tests/xunit và TRX của xUnit. Các tình huống cố tình lỗi audit chỉ tác động database tạm. Hướng dẫn: tests/README.md.

## Thử giao diện

Mở Chay-Web.cmd → Đăng ký → Đăng nhập → Hồ sơ. Mở Chay-QuanLy.cmd → Khách hàng để xem cùng dữ liệu, khóa/mở/reset. Quyền truy cập → Thiết bị POS để cấp/thu hồi theo hướng dẫn UC-ADM-02. Không dùng tài khoản hoặc đơn thật cho test hủy/khóa/reset tự động.

Mật khẩu tạm cũng dùng GUID thao tác, xác nhận mật khẩu phía API, version hồ sơ và receipt sau timeout; không nâng phiên hạn chế thành phiên đầy đủ. Kiểm thử Web mô phỏng mất phản hồi sau commit và xác nhận chỉ gửi một POST đổi mật khẩu tạm.
