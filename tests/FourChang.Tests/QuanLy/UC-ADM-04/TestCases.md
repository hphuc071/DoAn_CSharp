# UC-ADM-04 — Quản lý công thức pha chế BOM — Test cases

Nguồn: `D:\8.tailieu\dac-25-Uc.docx`; giữ số UC mới. Quy tắc liên quan: **BR-02, BR-03, BR-08, BR-11, BR-13, BR-14, BR-23, BR-24, BR-28, BR-29**. Tất cả test C# nằm trong project xUnit dùng chung, không tạo `.csproj` riêng theo UC.

| Mã TC | Tình huống | Kết quả cần đạt | Lớp xUnit |
|---|---|---|---|
| TC-ADM-04-01 | Nháp, clone và phiên bản đã công bố | Chỉ sửa/xóa nháp chưa dùng; công bố tạo phiên bản mới, giữ định mức cũ. | `RecipeAdministrationTests` |
| TC-ADM-04-02 | Mô phỏng định mức | Gộp FIXED/SUGAR/ICE, hao hụt, topping rồi làm tròn lên 4 số lẻ một lần; không trừ kho. | `RecipeAdministrationTests` |
| TC-ADM-04-03 | Lịch công bố và lịch chồng | Từ chối chồng lịch; worker chuyển đến ACTIVE đúng hạn, bản trước RETIRED. | `RecipeAdministrationTests` |
| TC-ADM-04-04 | Thiếu thành phần/đơn vị/quy đổi | Từ chối nhập/công bố; không tự tạo công thức hoặc thành phần giả. | `RecipeAdministrationTests` |

Kiểm tra WinForms: `AdminFeaturesUiTests` mở route thật với API, chụp desktop/compact và form nguyên liệu/BOM. Ảnh nằm trong `artifacts/tests/xunit/ADM01-05/<database-fixture>/`. `Theory` giao diện dùng `Layer=WinForms`, tham số UC được lưu trong tên ảnh thay vì gắn Trait động.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-04"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "FullyQualifiedName~AdminFeaturesUiTests"
```

Điều kiện chạy: Windows/.NET 8, SQL Server Docker đã hoạt động, `.env` local đã cấu hình và Playwright Chromium đã có cho test Browser. Fixture dùng database GUID riêng, không chạy lệnh xóa/reset trên database đồ án.

Kiểm tra thủ công bổ sung: ngắt API lúc form có nháp; xác nhận không báo lưu thành công, dữ liệu cũ có cảnh báo; nối lại và tra receipt/version trước gửi lại. Trước xóa/ngừng phải có xác nhận/lý do theo từng thao tác. Chạy SQL test không thay thế kiểm tra trực quan các dialog.
