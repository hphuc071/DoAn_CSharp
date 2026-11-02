# UC-ADM-02 — Quản lý quyền truy cập hệ thống

## Chạy kiểm thử

Từ thư mục gốc, Docker SQL Server đã chạy và solution đã build:

```powershell
pwsh -NoProfile -File tests/Run-Tests.ps1 -UseCase UC-ADM-02 -IncludePresentation -IncludeRegression
```

Runner tạo database tạm đúng 35 bảng; kiểm tra API, WinForms và API hồi quy; dọn database và tiến trình của chính lần chạy. `-KeepDatabase` chỉ dùng khi cần xem dữ liệu thử. Kết quả và ảnh giao diện nằm tại `artifacts/tests/QuanLy/UC-ADM-02/<run-id>/`.

## Dùng trên ứng dụng

1. Mở FourChang.Admin, đăng nhập Quản lý và chọn **Quyền truy cập**.
2. **Tài khoản Quản lý**: xem tài khoản duy nhất, đổi mật khẩu, đăng xuất. Mật khẩu tạm phải đổi trước Dashboard. Đổi mật khẩu yêu cầu đăng nhập lại; không đóng ca POS.
3. **Thiết bị POS**: đăng ký mã máy, tên máy, khóa riêng, lý do. Trên chính máy POS, chạy Admin để đăng ký máy đó và giữ chọn **Cấu hình POS trên máy Windows này**. Sau cấp, khóa chỉ xuất hiện trên form, không đọc lại từ server. Dùng cùng người dùng Windows khi chạy POS để đọc được DPAPI.
4. Thu hồi máy cần lý do. Ca đang mở vẫn giữ nguyên; dùng **Ca liên quan** để đối chiếu và chốt sau khi xử lý hết đơn còn dang dở. Cấp lại chỉ khi không còn ca chưa kết thúc.
5. **Phạm vi quyền** chỉ đọc: không cấp quyền Admin cho nhân viên, không tạo tài khoản nhân viên hoặc Quản lý thứ hai.

## Cấu trúc triển khai

API: `Features/Access/`; client + DTO: `Presentation.Shared/Integration/Access/`; màn hình: `Desktop/Features/Admin/Access/`; bảo vệ khóa: `Desktop/Security/PosDeviceCredentials.cs`; visual dùng chung UC-ADM-01: `Desktop/Controls/Admin/`.

Các thay đổi nhạy cảm có rowversion, mã thao tác chống gửi lặp và audit trong cùng giao dịch. Mất phản hồi phải tra kết quả cũ trước khi thử lại. Phiên đổi mật khẩu tạm chỉ truy cập phạm vi đổi mật khẩu; không vào Dashboard.
