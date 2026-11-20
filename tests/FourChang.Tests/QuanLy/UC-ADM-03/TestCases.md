# UC-ADM-03 Nguyên liệu và tồn kho

Các lớp kiểm thử trong thư mục này dùng project xUnit chung. Danh mục nguồn và bộ lọc nằm trong tests/test-manifest.json.

## Mã kiểm thử

- [IngredientAdministrationTests.cs](IngredientAdministrationTests.cs)
- [InventoryAdministrationTests.cs](InventoryAdministrationTests.cs)

## Chạy kiểm thử

~~~powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-03"
~~~

Test API và giao diện sử dụng database thử riêng khi cần SQL. Kết quả đạt cần căn cứ lượt chạy và tệp TRX tương ứng.

## Kịch bản kiểm thử 03

### Kiểm thử UC-ADM-03 — Quản lý nguyên liệu tinh gọn

BR liên quan: BR-03, BR-08, BR-23, BR-28, BR-29. Tái sử dụng IngredientAdministrationTests.cs và worker AdminFeaturesUiTests; không tạo project test mới.

| Trường hợp | Kết quả yêu cầu |
|---|---|
| Tìm/thêm/sửa hồ sơ g/ml | Giữ tên, mô tả, một ngưỡng và trạng thái; không tạo số dư kho |
| Gửi lại mã thao tác | Cùng payload chỉ ghi một lần; payload khác bị chặn |
| Phiên bản cũ | Thay đổi thực sự bị từ chối; không ghi đè hồ sơ mới |
| Ngưỡng 0, số lẻ, âm, quá 4 số lẻ | 0 và số hợp lệ lưu chính xác; âm/quá precision bị chặn |
| Dữ liệu tồn an toàn cũ | Không tạo ngưỡng thứ hai; hạ cảnh báo về0 được, không đổi số dư |
| Đơn vị ngoài g/ml, quy đổi tự khai báo | Từ chối; không tạo đơn vị/quy đổi |
| Đổi đơn vị đã tham chiếu | Chặn cả UI và API | Ngừng thiếu xác nhận hoặc dấu ảnh hưởng cũ | Từ chối, nguyên liệu vẫn đang dùng |
| Ngừng sau xác nhận mới | Món phụ thuộc không khả dụng; BOM và kho giữ nguyên |
| Kích hoạt lại | Khôi phục khả dụng nếu đủ điều kiện, không nhập thêm kho |
| Xóa từ client cũ | 409; giữ hồ sơ và lịch sử |
| Không ghi được audit | Rollback hồ sơ và sự kiện, không lưu dở dang |
| WinForms | Chỉ4cột,2nút, không tồn an toàn/quy đổi; thêm cả g/ml, giữ12,5; xác nhận ngừng có danh sách ảnh hưởng; footer không bị cắt |

Chạy cuối: adm03-simplified-final.trx8/8; adm03-ui-final.trx1/1 (UI được chạy lại sau xử lý lỗi preflight). Lượt adm03-simplified.trx ban đầu7/8: fixture sửa riêng trường tồn an toàn đã bị bỏ nên là no-op, không đổi rowversion; đã chỉnh fixture sửa ngưỡng thật và chạy lại toàn bộ8test đạt.

Fixture SQL GUID riêng, không xóa/reset database đồ án. Đã kiểm trực quan danh sách, form và hộp xác nhận. Kiểm mất mạng: chưa rõ kết quả phải giữ nháp, tra receipt/thử lại cùng mã, không báo thành công giả.

## Tìm kiếm không nhảy trang — 18/11/2026
WinForms nhập từng ký tự Sữa tươi, thêm khoảng trắng, lọc không có kết quả và xóa bộ lọc. Kiểm nguồn bảng không thay, kết quả giống không reset binding, khung bảng/ô nhập không đổi vị trí và focus không mất. ingredient-search-caret-final.trx1/1 đạt; ảnh ADM03-search-stable.png. Những lần kiểm trước phát hiện cuộn native24px đã được sửa; không tính các lượt lỗi là đạt.

## Kịch bản kiểm thử 05

### UC-ADM-03 — Theo dõi tồn kho và cảnh báo — Test cases

Nguồn: `D:\8.tailieu\dac-25-Uc.docx`; giữ số UC mới. Quy tắc liên quan: **BR-02, BR-03, BR-05, BR-08, BR-14, BR-23, BR-24, BR-26, BR-29, BR-35**. Tất cả test C# nằm trong project xUnit dùng chung, không tạo `.csproj` riêng theo UC.

| Mã TC | Tình huống | Kết quả cần đạt | Lớp xUnit |
|---|---|---|---|
| TC-ADM-05-01 | Đọc số dư/khả dụng/cảnh báo | Khả dụng = thực tế − giữ chỗ; thiếu số dư báo Chưa xác định. | `InventoryAdministrationTests` |
| TC-ADM-05-02 | Truy vết và chỉ đọc | Chi tiết đúng nguyên liệu và nguồn; GET không tạo biến động kho. | `InventoryAdministrationTests` |
| TC-ADM-05-03 | Ẩn danh và Customer | 401/403 trên API Admin; không chỉ dựa vào ẩn nút UI. | `InventoryAdministrationTests` |
| TC-ADM-05-04 | Tồn thay đổi trên Web đang mở | Trạng thái đặt món thay đổi theo nguồn chung; không tự chuyển thành ẩn. | `MenuSynchronizationBrowserTests` |

Kiểm tra WinForms: `AdminFeaturesUiTests` mở route thật với API, chụp desktop/compact và form nguyên liệu/BOM. Ảnh nằm trong `artifacts/tests/xunit/ADM01-05/<database-fixture>/`. `Theory` giao diện dùng `Layer=WinForms`, tham số UC được lưu trong tên ảnh thay vì gắn Trait động.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-03"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "FullyQualifiedName~AdminFeaturesUiTests"
```

Điều kiện chạy: Windows/.NET 8, SQL Server Docker đã hoạt động, `.env` local đã cấu hình và Playwright Chromium đã có cho test Browser. Fixture dùng database GUID riêng, không chạy lệnh xóa/reset trên database đồ án.

Kiểm tra thủ công bổ sung: ngắt API lúc form có nháp; xác nhận không báo lưu thành công, dữ liệu cũ có cảnh báo; nối lại và tra receipt/version trước gửi lại. Trước xóa/ngừng phải có xác nhận/lý do theo từng thao tác. Chạy SQL test không thay thế kiểm tra trực quan các dialog.
