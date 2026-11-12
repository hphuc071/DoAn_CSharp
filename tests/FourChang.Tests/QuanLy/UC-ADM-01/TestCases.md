# UC-ADM-01 — Quản lý tài khoản khách hàng — Test cases

Nguồn: `D:\8.tailieu\dac-25-Uc.docx`; giữ số UC mới. Quy tắc liên quan: **BR-06, BR-07, BR-08, BR-09, BR-13, BR-14, BR-28, BR-29**. Tất cả test C# nằm trong project xUnit dùng chung, không tạo `.csproj` riêng theo UC.

| Mã TC | Tình huống | Kết quả cần đạt | Lớp xUnit |
|---|---|---|---|
| TC-ADM-01-01 | Tạo/sửa, tìm kiếm, phân trang và tải lại | Định danh duy nhất; xóa từ khóa rồi tải lại trả danh sách không lọc. | `CustomerAdministrationTests` |
| TC-ADM-01-02 | Khóa/reset/ngừng và thu hồi phiên | Có lý do, version hợp lệ; Web không dùng phiên bị thu hồi; giữ lịch sử giao dịch. | `CustomerAdministrationTests` |
| TC-ADM-01-03 | Thêm/sửa/ngừng/chọn địa chỉ mặc định | Địa chỉ thuộc đúng khách, không mất địa chỉ đã chốt của đơn cũ. | `CustomerAddressAdministrationTests` |
| TC-ADM-01-04 | Số điện thoại 9/10/11 chữ số và ký tự khác số | Chỉ số gồm đúng 10 chữ số ASCII được lưu; SQL cũng chặn dữ liệu sai. | `CustomerPhoneTests` |
| TC-ADM-01-05 | Seed hai lần | Đúng 10 hồ sơ mẫu; lần hai không thêm trùng, không xóa dữ liệu hiện hữu. | `CustomerPhoneTests` |

Kiểm tra WinForms: `AdminFeaturesUiTests` mở route thật với API, chụp desktop/compact và form nguyên liệu/BOM. Ảnh nằm trong `artifacts/tests/xunit/ADM01-05/<database-fixture>/`. `Theory` giao diện dùng `Layer=WinForms`, tham số UC được lưu trong tên ảnh thay vì gắn Trait động.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-01"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "FullyQualifiedName~AdminFeaturesUiTests"
```

Điều kiện chạy: Windows/.NET 8, SQL Server Docker đã hoạt động, `.env` local đã cấu hình và Playwright Chromium đã có cho test Browser. Fixture dùng database GUID riêng, không chạy lệnh xóa/reset trên database đồ án.

Kiểm tra thủ công bổ sung: ngắt API lúc form có nháp; xác nhận không báo lưu thành công, dữ liệu cũ có cảnh báo; nối lại và tra receipt/version trước gửi lại. Trước xóa/ngừng phải có xác nhận/lý do theo từng thao tác. Chạy SQL test không thay thế kiểm tra trực quan các dialog.

## Bổ sung 10/11/2026

- `CustomerPhoneTests`: null/rỗng/khoảng trắng/9/11 số/ký tự/Unicode bị chặn; SQL NOT NULL không cho bỏ điện thoại; tạo/sửa đều bắt buộc 10 số.
- `CustomerPasswordResetUiTests`: form nhập mới/xác nhận/lý do, không khớp thì chưa lưu; cấp lại thành công thu hồi phiên cũ; mật khẩu cũ bị từ chối; khách đăng nhập bằng mật khẩu tạm, đổi tạm rồi truy cập hồ sơ đầy đủ. Test nằm đúng UC-ADM-01, dùng chung worker STA.
- `CustomersUiTests` được giữ lại để kiểm tra mất phản hồi, tạo/sửa/tìm/phân trang/tải lại; dữ liệu dương được bổ sung số hợp lệ, không bỏ các assertion cũ.
