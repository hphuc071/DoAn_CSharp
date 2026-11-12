# UC-ADM-05 — Theo dõi tồn kho và cảnh báo — Test cases

Nguồn: `D:\8.tailieu\dac-25-Uc.docx`; giữ số UC mới. Quy tắc liên quan: **BR-02, BR-03, BR-05, BR-08, BR-14, BR-23, BR-24, BR-26, BR-29, BR-35**. Tất cả test C# nằm trong project xUnit dùng chung, không tạo `.csproj` riêng theo UC.

| Mã TC | Tình huống | Kết quả cần đạt | Lớp xUnit |
|---|---|---|---|
| TC-ADM-05-01 | Đọc số dư/khả dụng/cảnh báo | Khả dụng = thực tế − giữ chỗ − an toàn; thiếu số dư báo Chưa xác định. | `InventoryAdministrationTests` |
| TC-ADM-05-02 | Truy vết và chỉ đọc | Chi tiết đúng nguyên liệu và nguồn; GET không tạo biến động kho. | `InventoryAdministrationTests` |
| TC-ADM-05-03 | Ẩn danh và Customer | 401/403 trên API Admin; không chỉ dựa vào ẩn nút UI. | `InventoryAdministrationTests` |
| TC-ADM-05-04 | Tồn thay đổi trên Web đang mở | Trạng thái đặt món thay đổi theo nguồn chung; không tự chuyển thành ẩn. | `MenuSynchronizationBrowserTests` |

Kiểm tra WinForms: `AdminFeaturesUiTests` mở route thật với API, chụp desktop/compact và form nguyên liệu/BOM. Ảnh nằm trong `artifacts/tests/xunit/ADM01-05/<database-fixture>/`. `Theory` giao diện dùng `Layer=WinForms`, tham số UC được lưu trong tên ảnh thay vì gắn Trait động.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-05"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "FullyQualifiedName~AdminFeaturesUiTests"
```

Điều kiện chạy: Windows/.NET 8, SQL Server Docker đã hoạt động, `.env` local đã cấu hình và Playwright Chromium đã có cho test Browser. Fixture dùng database GUID riêng, không chạy lệnh xóa/reset trên database đồ án.

Kiểm tra thủ công bổ sung: ngắt API lúc form có nháp; xác nhận không báo lưu thành công, dữ liệu cũ có cảnh báo; nối lại và tra receipt/version trước gửi lại. Trước xóa/ngừng phải có xác nhận/lý do theo từng thao tác. Chạy SQL test không thay thế kiểm tra trực quan các dialog.
