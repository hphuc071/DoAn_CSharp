# UC-ADM-02 — Quản lý thực đơn — Test cases

Nguồn: `D:\8.tailieu\dac-25-Uc.docx`; giữ số UC mới. Quy tắc liên quan: **BR-03, BR-05, BR-08, BR-11, BR-12, BR-13, BR-14, BR-24, BR-28, BR-29, BR-35**. Tất cả test C# nằm trong project xUnit dùng chung, không tạo `.csproj` riêng theo UC.

| Mã TC | Tình huống | Kết quả cần đạt | Lớp xUnit |
|---|---|---|---|
| TC-ADM-02-01 | Danh mục, món, cấu hình, topping | Giá lấy từ cấu hình chung, kiểm tra trùng/version/tham chiếu. | `MenuAdministrationTests` |
| TC-ADM-02-02 | Công bố, BOM thiếu, lịch và banner | Chỉ công bố cấu hình hợp lệ; không giả định thiếu BOM bằng 0. | `MenuAdministrationTests` |
| TC-ADM-02-03 | Audit lỗi/gửi lại sau mất phản hồi | Rollback nguyên tử hoặc trả receipt cũ; không nhân đôi lệnh. | `MenuAdministrationTests` |
| TC-ADM-02-04 | Admin đổi giá/tồn, Web đang mở | DOM cập nhật giá/trạng thái; giỏ nháp được giữ. | `MenuSynchronizationBrowserTests` |
| TC-ADM-02-05 | Tắt bán và ẩn | Tắt bán vẫn hiện Tạm hết; chỉ bỏ công bố/ẩn theo thao tác Admin mới mất khỏi menu. | `MenuSynchronizationBrowserTests` |

Kiểm tra WinForms: `AdminFeaturesUiTests` mở route thật với API, chụp desktop/compact và form nguyên liệu/BOM. Ảnh nằm trong `artifacts/tests/xunit/ADM01-05/<database-fixture>/`. `Theory` giao diện dùng `Layer=WinForms`, tham số UC được lưu trong tên ảnh thay vì gắn Trait động.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-02"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "FullyQualifiedName~AdminFeaturesUiTests"
```

Điều kiện chạy: Windows/.NET 8, SQL Server Docker đã hoạt động, `.env` local đã cấu hình và Playwright Chromium đã có cho test Browser. Fixture dùng database GUID riêng, không chạy lệnh xóa/reset trên database đồ án.

Kiểm tra thủ công bổ sung: ngắt API lúc form có nháp; xác nhận không báo lưu thành công, dữ liệu cũ có cảnh báo; nối lại và tra receipt/version trước gửi lại. Trước xóa/ngừng phải có xác nhận/lý do theo từng thao tác. Chạy SQL test không thay thế kiểm tra trực quan các dialog.
