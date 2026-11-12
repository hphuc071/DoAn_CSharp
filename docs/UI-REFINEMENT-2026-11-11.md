# UI Refinement — 11/11/2026

## Phạm vi và nhận diện

Áp dụng hướng dẫn từ skill `C:/Users/gauba/.codex/skills/.agents/skills/ui-ux-pro-max/SKILL.md` cho các màn hình trong chín ảnh phản hồi. Giữ tên **4CHANG LINH NGU LAM**, logo và vị trí sidebar/header, khu vực món, vùng tùy chỉnh, bộ lọc và bảng.

Màu gốc được tái sử dụng: nâu `#75452D`, nâu đậm `#30241D`, kem `#F7F2EA`, surface `#FFFDF9`, viền `#E5DCD1`, chữ `#2C2621`. Không thêm chế độ đổi theme, font tải ngoài hoặc thư viện UI mới.

## Vấn đề và cách khắc phục

| Vấn đề | Thay đổi |
|---|---|
| Card bộ lọc/toolbar cao dần, đẩy bảng xuống dưới | Bỏ đo chiều cao bằng tọa độ cũ trong sự kiện Layout; dùng GrowAndShrink và phép đo theo chiều rộng thực của vùng chứa. |
| Hàng lọc thứ hai bị cắt khi cửa sổ hẹp | `ContentFlowPanel` giới hạn chiều rộng trong GetPreferredSize; kiểm tra cả bounds của control bên trong card sau nhiều lần resize. |
| Nút tìm kiếm/tải lại bị tách hàng | Đặt trong cùng nhóm, giữ thứ tự Click và xử lý hiện hữu. |
| Ô tìm kiếm nguyên bản lệch các ô khác | Dùng nhãn phía trên và input frame chung, căn nút với ô nhập. |
| Bảng lấn vào bộ đếm | Tính chiều cao tối thiểu của grid cộng thanh loading. |
| Biểu mẫu phiếu kho dài và khó quan sát | Nhóm các trường liên quan theo ba cột; lý do dùng cả chiều rộng; giữ Lưu/Hủy ngoài vùng cuộn. |
| Số tồn có nhiều số 0 thập phân | Chỉ định dạng hiển thị `#,##0.####`, không làm tròn dữ liệu hoặc phép tính. |
| Web Orders dồn nhãn/ô ngày/nút trên một dòng | Grid bộ lọc 4/2/1 cột theo màn hình; nhãn riêng, trạng thái rỗng rõ ràng, phân trang và thời điểm đọc dễ thấy. |
| Menu thiếu nhịp khoảng cách và vùng nhấn nhỏ | Khoảng cách 8/16/24px, card bo góc thống nhất, nút thêm 44px, typography rõ và form topping có nhóm. |
| Footer Web nằm giữa trang trống | Flex layout theo chiều cao viewport, nội dung vẫn được cuộn tự nhiên. |

Giữ các hiệu ứng hover/pressed/focus nhẹ hiện hữu; chuẩn hóa vùng tùy chỉnh. Các trang dùng `AdminWorkspacePage` có phản hồi loading, timer chỉ chạy khi tải. Web tắt chuyển động qua `prefers-reduced-motion`.

## File sửa trong đợt này

Đường dẫn dưới đây tính từ gốc repository:

### Desktop

- `src/Presentation/FourChang.Desktop/Shared/Controls/ContentFlowPanel.cs` — mới; đo chiều cao theo chiều rộng thật.
- `src/Presentation/FourChang.Desktop/Shared/Controls/Ui.cs`
- `src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminVisuals.cs`
- `src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminListLayout.cs`
- `src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminWorkspacePage.cs`
- `src/Presentation/FourChang.Desktop/Shared/Controls/Admin/OperationsPresentation.cs`
- `src/Presentation/FourChang.Desktop/Admin/Recipes/RecipesPage.cs`
- `src/Presentation/FourChang.Desktop/Admin/Inventory/InventoryPage.cs`
- `src/Presentation/FourChang.Desktop/Admin/Orders/OrdersManagementPage.cs`
- `src/Presentation/FourChang.Desktop/Admin/Invoices/InvoicesManagementPage.cs`
- `src/Presentation/FourChang.Desktop/Admin/StockDocuments/StockDocumentEditorForm.cs`

Trang Chứng từ kho và Hoàn tiền hưởng sửa lỗi thông qua khung dùng chung; không sửa các lệnh ghi nghiệp vụ của chúng.

### Website

- `src/Presentation/FourChang.Web/Pages/Orders/Index.cshtml`
- `src/Presentation/FourChang.Web/Pages/Menu/Index.cshtml`
- `src/Presentation/FourChang.Web/Pages/Shared/_Layout.cshtml`
- `src/Presentation/FourChang.Web/Pages/Shared/_ProductCustomizer.cshtml`
- `src/Presentation/FourChang.Web/wwwroot/css/refresh.css`
- `src/Presentation/FourChang.Web/wwwroot/js/cart-editor.js` — thay duy nhất nhãn tiêu đề trình bày.

### Test

- `tests/FourChang.Tests/QuanLy/DungChung/AdminFeaturesUiTests.cs` — kiểm tra ancestor clipping, nhiều lần resize và bounds grid.
- `tests/FourChang.Tests/TichHop/InterfaceRefinementBrowserTests.cs` — mới; Web thật với API/SQL fixture, ảnh desktop/mobile, lọc/bỏ lọc, bàn phím, tùy chỉnh/thêm giỏ và reduced motion.

## Bảo toàn nghiệp vụ

Không thay database, dữ liệu quán, API contract, phân quyền, quy tắc giá/thu tiền/hoàn tiền/kho hoặc mở thêm giao hàng/QR. Các kiểm thử có ghi dữ liệu chỉ sử dụng database fixture riêng và tự dọn dẹp. Không commit/push hoặc sửa các thay đổi của thành viên khác.

## Cách xem

- Web: http://localhost:15880/ — nhấn Ctrl+F5 nếu trình duyệt còn cache.
- Quản lý: mở lại `Chay-QuanLy.cmd` sau khi đóng phiên ứng dụng cũ bình thường.
- Backend: http://localhost:15881/health/ready.

Ảnh kiểm chứng nằm trong `artifacts/tests/xunit/UIRefinement-Menu`, `UIRefinement-Orders` và `ADM01-05`, mỗi lần chạy có thư mục riêng.

## Kiểm tra

- `dotnet build FourChang.sln --no-restore`: thành công, **0 warning, 0 error**.
- Desktop cuối: **11/11 đạt**, ảnh thật ở hai kích thước 1430×960 và 1280×850; resize nhiều lần 1430→1280→1500→1280. Đối chiếu ảnh của BOM, tồn kho, chứng từ kho, đơn hàng, hóa đơn, hoàn tiền và editor.
- Web thật: **2/2 đạt**, tại 1440/1024/768/390/375px; không tràn ngang, lọc/bỏ lọc, Tab focus, tùy chỉnh/thêm giỏ và prefers-reduced-motion hoạt động.
- Hồi quy Web: **26 trường hợp khác nhau đạt** (CartDraftBrowserTests, MainMenuIntegrationBrowserTests, OrderSnapshotBrowserTests).
- Tổng hợp kết quả mới nhất theo tên từng test: **39/39 trường hợp đạt qua các đợt xác nhận**. Đây là kết quả tổng hợp, không phải một lần chạy duy nhất: đợt 39 ca đầu có một lần Edge đóng khi khởi động; nhóm MainMenu chạy lại đạt 6/6. Phép đo grid mới phát hiện thanh loading 3px chưa được cộng vào chiều cao; đã sửa và chạy lại Desktop đạt 11/11.
- Web đang chạy tại 15880; `/`, `/css/refresh.css`, `/assets/brand-mark.svg` đều HTTP 200; CSS chứa block refinement mới. API tại 15881 báo ready với 37 bảng.
- Không có lỗi còn lại trong phạm vi hình học và các ca đã kiểm tra. Chưa thể suy ra toàn bộ nghiệp vụ của 25 UC đã được kiểm thử chỉ từ đợt UI này.

Báo cáo máy đọc: `tests/FourChang.Tests/TestResults/ui-refinement-final.trx`, `ui-refinement-confirmation.trx`, `ui-refinement-layout-final.trx`.

Ảnh xem nhanh: [Đơn hàng Web](previews/ui-refinement-20261111/web-orders-desktop.png), [Menu điện thoại](previews/ui-refinement-20261111/web-menu-mobile.png), [Đơn hàng Admin](previews/ui-refinement-20261111/admin-orders-desktop.png).
