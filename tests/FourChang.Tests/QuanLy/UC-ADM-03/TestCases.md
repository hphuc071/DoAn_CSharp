# UC-ADM-03 — Quản lý nguyên liệu — Test cases

Nguồn: `D:\8.tailieu\dac-25-Uc.docx`; giữ số UC mới. Quy tắc liên quan: **BR-03, BR-08, BR-14, BR-23, BR-24, BR-28, BR-29**. Tất cả test C# nằm trong project xUnit dùng chung, không tạo `.csproj` riêng theo UC.

| Mã TC | Tình huống | Kết quả cần đạt | Lớp xUnit |
|---|---|---|---|
| TC-ADM-03-01 | CRUD nguyên liệu và đơn vị/quy đổi | Đơn vị cơ sở có hệ số 1; không nhận/sửa số dư kho từ form danh mục. | `IngredientAdministrationTests` |
| TC-ADM-03-02 | Bản cũ hoặc lặp mã thao tác | Bản cũ bị chặn; cùng lệnh cùng nội dung trả lại kết quả cũ. | `IngredientAdministrationTests` |
| TC-ADM-03-03 | Nguyên liệu/đơn vị đã phát sinh | Chặn xóa/đổi đơn vị cơ sở hoặc ngừng khi còn BOM/giữ chỗ. | `IngredientAdministrationTests` |
| TC-ADM-03-04 | Ngưỡng/hệ số sai và audit lỗi | Không có dữ liệu/quy đổi/outbox cập nhật một phần. | `IngredientAdministrationTests` |

Kiểm tra WinForms: `AdminFeaturesUiTests` mở route thật với API, chụp desktop/compact và form nguyên liệu/BOM. Ảnh nằm trong `artifacts/tests/xunit/ADM01-05/<database-fixture>/`. `Theory` giao diện dùng `Layer=WinForms`, tham số UC được lưu trong tên ảnh thay vì gắn Trait động.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-03"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "FullyQualifiedName~AdminFeaturesUiTests"
```

Điều kiện chạy: Windows/.NET 8, SQL Server Docker đã hoạt động, `.env` local đã cấu hình và Playwright Chromium đã có cho test Browser. Fixture dùng database GUID riêng, không chạy lệnh xóa/reset trên database đồ án.

Kiểm tra thủ công bổ sung: ngắt API lúc form có nháp; xác nhận không báo lưu thành công, dữ liệu cũ có cảnh báo; nối lại và tra receipt/version trước gửi lại. Trước xóa/ngừng phải có xác nhận/lý do theo từng thao tác. Chạy SQL test không thay thế kiểm tra trực quan các dialog.

## Điều chỉnh đơn vị cố định — 10/11/2026

Yêu cầu mới bỏ CRUD đơn vị/quy đổi: thay test CRUD đơn vị bằng test các route ghi đều bị chặn, dữ liệu hai đơn vị không đổi. Thêm test không nhận kg hoặc hệ số/cặp quy đổi tự khai báo. Giữ test tham chiếu/version/rollback và thử tạo nguyên liệu g/ml qua WinForms, mở sửa giữ đúng đơn vị. BOM chọn nguyên liệu lỏng thì tự hiển thị ml, không cho chọn đơn vị khác. Xem docs/INGREDIENTS-FIXED-UNITS.md.
