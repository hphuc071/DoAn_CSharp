# Bàn giao UC-ADM-03

Đã bổ sung đúng hai bảng và ba cột được phê duyệt; triển khai quản trị thực đơn thật, chia sẻ giá/cấu hình/topping/trạng thái với Web và POS. Giữ thương hiệu, palette, sidebar/header; không đổi đơn, thanh toán, kho, phân quyền hoặc snapshot cũ.

## Database đã nâng cấp

- 37 bảng nghiệp vụ, 73 FK. `sysdiagrams` giữ nguyên, nằm ngoài số bảng nghiệp vụ.
- `Banner`, `SuKienDongBo`; `Mon.CongBoTuLuc`, `CauHinhMon.LaMacDinh`, `TuyChonPhaCheHopLe.LaMacDinh`.
- Có backup COPY_ONLY và RESTORE VERIFYONLY trước nâng cấp. Đối chiếu count/checksum trước/sau: nhân viên, khách, ca, địa chỉ, đơn, dòng đơn, topping, audit, giá cấu hình và thông tin món cũ không đổi. Chỉ lịch/mặc định và rowversion liên quan được bổ sung theo migration.
- Dữ liệu mẫu trước đây được giữ; không thêm banner/sự kiện giả để làm đầy bảng mới. Sự kiện xuất hiện khi Admin lưu thực đơn.

## Chức năng

- Admin → Thực đơn: năm tab Món, Cấu hình món, Danh mục, Topping, Banner. Tìm theo mã/tên; thêm, sửa, xóa có điều kiện; xác nhận khi ẩn danh mục có món. Giá theo size/nhiệt độ, cặp đường/đá, mặc định, giới hạn topping, trạng thái công bố/bật bán và lịch.
- Giữ nháp khi lỗi; timeout tra receipt/cùng mã để không tạo trùng. Ghi menu + audit + outbox nguyên tử. Xóa dữ liệu đã tham chiếu bị chặn.
- Web/POS đọc cùng `/api/catalog`; SSE/change feed SQL báo tải lại khi đổi menu hoặc đến mốc lịch. Reconnect tải snapshot, giữ ghi chú/từ khóa; báo khi giá/lựa chọn đổi. Bật bán không vượt kiểm tra nguyên liệu/BOM.
- Web có ảnh/tùy chọn/topping và giá từ API. POS có hộp chọn cấu hình/đường/đá/topping. Không làm giả UC tạo đơn/thu tiền/giữ kho chưa triển khai.
- BOM được tạo/kích hoạt ở ADM-05; ADM-03 không tự tạo công thức để ép công bố. Món/topping mới phải hoàn thiện BOM hợp lệ trước khi công bố/kích hoạt.

## Cấu trúc và các file chính

| Nhóm | Đường dẫn |
|---|---|
| Schema/manifest/nâng cấp | docs/database/37-bang/ |
| Entities/EF | src/Backend/FourChang.Domain/Entities/Menu/; src/Backend/FourChang.Infrastructure/Database/Configurations/Menu/; Context/CafeDbContext.cs; Initialization/DatabaseBootstrap.cs, DemoSeed.cs |
| Backend ADM-03 | src/Backend/FourChang.Api/Features/Admin/Menu/ — MenuAdminService, MenuAdminEndpoints, MenuValidation, CatalogReadService, MenuOutboxWorker |
| API cũ tích hợp | src/Backend/FourChang.Api/Features/Customer/Menu/CatalogEndpoints.cs; Api/Program.cs và csproj |
| DTO/client dùng chung | src/Shared/FourChang.Presentation.Shared/Contracts/Admin/MenuContracts.cs; Contracts/Customer/CatalogContracts.cs; ApiClients/Admin/MenuClient.cs |
| Admin WinForms | src/Presentation/FourChang.Desktop/Admin/Menu/ — MenuManagementPage, MenuEditorForm, MenuManagementView; Shared/Database/DatabaseViews.cs |
| POS | src/Presentation/FourChang.Desktop/Staff/Sales/SalePage.cs, CatalogSelectionForm.cs; Shared/Controls/CatalogImage.cs |
| Web | Pages/Menu/Index.cshtml(.cs); Pages/Shared/_ProductCustomizer.cshtml; Services/CatalogProxy.cs; Program.cs; wwwroot/js/catalog.js, site.js; wwwroot/css/refresh.css |
| Kiểm thử | tests/FourChang.Tests/QuanLy/UC-ADM-03/; Program.cs; DungChung/NetworkFaultProxy.cs; DatabaseTests và fixture EmployeeContactAddressTests; assert số bảng CustomerAdministrationTests |
| Cấu hình/phối hợp | .editorconfig; Infrastructure.csproj; scripts/Start-LocalBackend.ps1; tests/test-manifest.json; docs/INTEGRATION-ADM-03.md |

Mỗi phương thức mới có chú thích chức năng/lý do; vẫn dùng một project xUnit. Không thêm framework hoặc package.

## Kiểm chứng

- Solution build: 0 warning, 0 error.
- 10/10 test ADM-03 đạt; cùng 2 test schema tạo mới/nâng cấp đạt.
- Hồi quy toàn bộ: 43/44 đạt ở lượt toàn bộ. Test fixture địa chỉ cũ đã sửa để dựng đúng schema 35 thay vì schema 37 thiếu cột; chạy lại cùng hai test Web liên quan đạt 3/3. Kết quả gần nhất của 44 test phân biệt đều đạt (không khẳng định một lượt duy nhất 44/44).
- Đã xem ảnh năm tab/năm editor Admin, POS menu/chọn cấu hình và Website 375/768/1440. Sửa footer nút Lưu bị xuống hàng, cột mã quá hẹp, topping mobile bị chia ô, badge bị ảnh che; giữ skip link hoạt động khi focus. Không kết luận chỉ từ build.
- Bằng chứng: `artifacts/tests/adm-03/`, `artifacts/adm-03/verification.json`, `artifacts/adm-03/ui-review/`. GUID fixture và các giá thử nghiệm trong ảnh không phải dữ liệu đang bán của database đồ án.

## Tài liệu tiếp theo

- [Đặc tả gốc và mapping BR/luồng/test](use-cases/UC-ADM-03.md).
- [Hướng dẫn tích hợp cho nhóm/GitHub](INTEGRATION-ADM-03.md).
- [Database hiện hành](database/37-bang/README.md).

Chưa push/merge lên GitHub. Các UC ngoài ADM-03 tiếp tục giữ phạm vi hiện có. Ảnh HTTPS chỉ kiểm tra cú pháp; ảnh không tải được được bỏ qua an toàn. Xác nhận đơn sau này phải tái kiểm tra giá/tồn, không tin tạm tính hoặc thông báo SSE của client.
