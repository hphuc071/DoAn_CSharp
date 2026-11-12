# Triển khai ADM-01 đến ADM-05 theo đặc tả 25 UC

## Phạm vi và quyết định đã xác nhận

- Nguồn yêu cầu: `dac-25-Uc.docx`; ADM-01 khách hàng, ADM-02 thực đơn, ADM-03 nguyên liệu, ADM-04 BOM, ADM-05 tồn kho. Không dùng số ADM-03 cũ cho thực đơn.
- Nhánh triển khai: `feature/adm01-05-spec25`, bắt đầu từ main `a6e5761`. Đã đối chiếu nhánh `CSharp/feature/QuanLy`; chỉ chọn phần liên quan, không merge nguyên nhánh đè code đặt hàng/thanh toán trên main.
- Giữ thương hiệu 4CHANG LINH NGU LAM, logo, màu nâu–kem và cấu trúc src Backend/Presentation/Shared.
- Quyết định chủ dự án: **tắt bán vẫn hiện Tạm hết; chỉ ẩn khi Admin chủ động bỏ công bố/ẩn**. Thiếu nguyên liệu vẫn hiện Tạm hết. Đây là cách áp dụng BR-03 sau xác nhận, ưu tiên hơn cách diễn đạt cũ “ngừng kinh doanh phải ẩn”.
- 10 khách hàng mẫu có tên Việt Nam tự nhiên, điện thoại giả lập đủ 10 chữ số, email `example.test`. Chỉ thêm nếu chưa có; không reset ID hoặc xóa dữ liệu/giao dịch cũ.
- Điện thoại khách bắt buộc có giá trị và đúng 10 chữ số ASCII (yêu cầu bổ sung ngày 10/11/2026). Điện thoại người nhận bắt buộc đủ 10 chữ số. Không tự chuyển +84 hoặc cắt/thêm số.
- Đã sao lưu và kiểm tra backup trước khi sửa hai số cũ được duyệt: khách mã 1 → 0907999001; địa chỉ mã 20002 → 0907999002. Ghi audit SYSTEM trước/sau, không sửa dòng khác.

## Các lớp mã nguồn

| Nơi lưu | Trách nhiệm |
|---|---|
| `src/Backend/FourChang.Api/Features/Admin/Customers` | Hồ sơ/địa chỉ, quyền, validation, giao dịch, thu hồi phiên |
| `src/Backend/FourChang.Api/Features/Admin/Menu` | Giá và công bố dùng chung, banner, outbox, SSE |
| `src/Backend/FourChang.Api/Features/Admin/Ingredients` | Danh mục, đơn vị, quy đổi, ngưỡng; không sửa số dư |
| `src/Backend/FourChang.Api/Features/Admin/Recipes` | Phiên bản, mô phỏng, công bố/lịch, worker kích hoạt |
| `src/Backend/FourChang.Api/Features/Admin/Inventory` | Số dư nhất quán và truy vết chỉ đọc |
| `src/Backend/FourChang.Api/Shared/Operations` | Transaction, app-lock, rowversion, chống trùng, audit |
| `src/Backend/FourChang.Infrastructure/Database` | Mapping/ràng buộc, nâng cấp có kiểm tra, seed tường minh |
| `src/Shared/FourChang.Presentation.Shared/Contracts/Admin` | DTO dùng chung; không khai báo giá riêng ở client |
| `src/Shared/FourChang.Presentation.Shared/ApiClients/Admin` | HTTP client cho UI, receipt và lỗi có cấu trúc |
| `src/Presentation/FourChang.Desktop/Admin` | Form/page từng UC, nháp và xác nhận thao tác |
| `src/Presentation/FourChang.Desktop/Shared/Controls/Admin` | Khung danh sách, dialog, feedback lỗi/tải |
| `src/Presentation/FourChang.Web/Services/CatalogUpdateEndpoints.cs` | Proxy same-origin cho snapshot/SSE, giữ contract Web hiện có |
| `tests/FourChang.Tests/QuanLy/UC-ADM-01..05` | Test theo UC trong một project xUnit |

Các phương thức nghiệp vụ mới có XML summary; các đoạn giao dịch, version và retry có chú thích lý do. Component `Ui.Field` chốt chiều rộng trước khi bọc input để sửa lỗi giãn đồng loạt làm lệch nút. Form BOM thu gọn bảng, thêm nhãn trường; nguyên liệu hiển thị trạng thái tiếng Việt.

## Database và dữ liệu mẫu

Giữ **37 bảng nghiệp vụ**, không thêm bảng trong lần ADM-01..05 này. Cổng nâng cấp nhận diện bộ 35 cũ đã chốt, thêm hai bảng Banner/SuKienDongBo theo phần đã được duyệt trước đây; sau đó áp dụng `004-customer-phone-rules.sql` với hai CHECK điện thoại. Có `sysdiagrams` thì nhận diện riêng, không xóa. Mô hình EF và manifest SQL cùng khai báo CHECK.

```powershell
dotnet run --project src/Backend/FourChang.Api/FourChang.Api.csproj -- --init-db
dotnet run --project src/Backend/FourChang.Api/FourChang.Api.csproj -- --check-db
dotnet run --project src/Backend/FourChang.Api/FourChang.Api.csproj -- --seed-customers
```

`--seed-customers` không chạy mỗi lần mở API. Tên đăng nhập `mau.*`, điện thoại 0907000001…0907000010. Không công bố mật khẩu dùng chung: seed băm mật khẩu ngẫu nhiên, Admin dùng reset hợp lệ nếu cần đăng nhập mẫu. Khách hiện hữu và lịch sử được giữ nguyên.

## Tích hợp với thành viên khác

1. Tạo PR nhánh này vào nhánh tích hợp của nhóm. Rà diff trước merge; không chọn toàn bộ ours/theirs khi xung đột Program.cs, DbContext, CatalogEndpoints, DTO hoặc JS giỏ hàng.
2. Merge Backend/Domain/Infrastructure và Shared Contracts/ApiClients cùng phần Presentation liên quan. Cùng ID món/cấu hình/topping, giá và trạng thái; không sao chép giá vào WinForms/JS.
3. Mỗi máy tự cấu hình `.env` theo `.env.example`; file thật phải được Git bỏ qua. Không push `.env`, mật khẩu, token POS, backup SQL hoặc artifacts test.
4. Sao lưu database máy đích. Chạy `--init-db`, rồi `--check-db`. Khi script báo số cũ sai, yêu cầu chủ dữ liệu quyết định; không tự chuẩn hóa bằng cách cắt số. Giữ các script/model 35 và 37 để nhận diện phiên bản.
5. Chạy test mục tiêu trước khi chạy chung với nhánh Web/POS. Web giữ contract `/api/menu/*`; snapshot `/api/catalog` và SSE cập nhật cùng nguồn. POS polling catalog không xóa đơn đang thao tác. Giỏ Web kiểm tra lại cấu hình/giá trước lưu, giữ nháp khi cần xác nhận giá mới.
6. Kiểm thử tích hợp: Admin đổi giá → Web/POS thấy cùng giá; tắt bán → vẫn hiện Tạm hết; bỏ công bố → ẩn; đổi tồn/giữ chỗ → khả dụng cập nhật; BOM mới không sửa nguyên liệu snapshot của đơn cũ.

```powershell
git switch feature/adm01-05-spec25
git diff --check
git status --short
# Sau khi tự rà soát diff và test, chọn file cần commit; không add secrets/artifacts.
git add src tests docs
git commit -m "Implement ADM-01 to ADM-05 from 25-UC specification"
git push -u CSharp feature/adm01-05-spec25
```

Chưa tự commit/push hoặc merge vào main. Tên remote thực tế cần kiểm tra bằng `git remote -v` trước khi dùng lệnh trên.

## Kiểm chứng và giới hạn

- Test API/SQL thật tạo database GUID cô lập; test WinForms chụp màn hình desktop/compact; browser kiểm tra đồng bộ menu, giữ giỏ nháp và mobile.
- Kết quả và số test từng lượt xem bảng kiểm tra cuối ở dưới; artifacts giữ local để giảng viên/nhóm có thể tái chạy.
- Chỉ triển khai năm UC này; chứng từ kho ADM-06 chỉ được điều hướng tới màn hình có sẵn, không khẳng định đã hoàn thiện nghiệp vụ ADM-06.
- Việc loại bỏ xác thực thiết bị POS và bảng ThietBiPOS không thuộc phạm vi năm UC này; không âm thầm thay bảo mật hay xóa bảng. Cơ chế POS hiện hữu được giữ.
- Dashboard/nhân viên/ca và các UC khác không được đánh dấu hoàn thành chỉ vì tồn tại giao diện. Manifest test đúng 25 mã trong tài liệu; có test không đồng nghĩa mọi luồng đã hoàn tất.

## Xác nhận khi chạy hồi quy

- Chủ dự án đồng ý giữ logic báo giá hiện có trên main: TAKEAWAY được báo giá, DELIVERY chưa có tuyến/phí xác minh bị chặn 400. Test cũ kỳ vọng DELIVERY 200 được cập nhật thành hai nhánh này; không sửa `OrderPricingService` hay mở thêm nghiệp vụ giao hàng.
- Đã sửa listener giỏ hàng theo delegation trên document, chỉ nhận nút trong product-card. Hỗ trợ markup cũ không có product-grid và card thêm động, không thay lưu trữ hay tính tiền.
- Test host bộ lọc menu đăng ký `CatalogReadService` là dependency mới của nguồn menu chung; connection forbidden vẫn giữ nguyên để bảo đảm validation không truy cập SQL.

## Kết quả kiểm tra cuối — 10/11/2026

Build `FourChang.sln`: **0 lỗi, 0 cảnh báo**. Các lượt chạy lại dưới đây có thể lặp các test đã chạy ở lượt mục tiêu; không cộng các lượt thành số test duy nhất.

| Bộ kiểm tra | Kết quả | Bằng chứng local (không commit artifacts) |
|---|---|---|
| API/SQL/Browser/WinForms của 5 UC | 39/39 đạt | `artifacts/tests/final/adm01-05-final.trx` |
| Hồi quy tài khoản, hồ sơ, menu, giỏ và database | 33/33 đạt | `artifacts/tests/final/adm01-05-regression.trx` |
| Chạy lại 5 màn hình sau sửa footer/hiển thị BOM | 5/5 đạt | `artifacts/tests/final/adm01-05-ui-final.trx` |
| Chạy lại CHECK/FK/upgrade/readiness | 2/2 đạt | `artifacts/tests/final/database-final.trx` |

Đã xem trực quan ảnh năm màn hình Admin, dialog nguyên liệu/BOM và Web mobile. Nút Lưu/Hủy được giữ ngoài vùng cuộn, test kiểm tra nút nằm trong ClientRectangle ở kích thước thường và tối thiểu. Trạng thái BOM hiển thị tiếng Việt, giờ UTC được đổi sang giờ địa phương khi hiển thị; không sửa lịch lưu trữ.

Kiểm tra database đồ án thực tế: 37 bảng nghiệp vụ, 10 khách mẫu mới, 0 điện thoại khách sai, 0 điện thoại địa chỉ sai, 2 CHECK mới đang bật và trusted. Bản sao lưu đã `RESTORE VERIFYONLY` trước sửa hai bản ghi: `/var/opt/mssql/backup/FourChangDb_before_approved_phone_repair_20261110_112645.bak` trong container SQL.

Script `Start-LocalBackend.ps1` đã sửa mốc readiness 35 → 37. Đã chạy script thật, nhận `status=ready, tables=37` tại `http://localhost:5181/health/ready`. Khi pull thay đổi, build lại để không chạy binary cũ:

```powershell
dotnet build FourChang.sln
.\Chay-QuanLy.cmd
```

Chạy test mục tiêu bằng xUnit (một project dùng chung):

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-01|UseCase=UC-ADM-02|UseCase=UC-ADM-03|UseCase=UC-ADM-04|UseCase=UC-ADM-05|FullyQualifiedName~AdminFeaturesUiTests"
```

## Đối chiếu quy tắc và triển khai

| UC | Quy tắc trong tài liệu | Chỗ triển khai chính | Kiểm chứng |
|---|---|---|---|
| UC-ADM-01 | BR-06, BR-07, BR-08, BR-09, BR-13, BR-14, BR-28, BR-29 | `Features/Admin/Customers`, Desktop `Admin/Customers`, Shared contracts/client | `tests/FourChang.Tests/QuanLy/UC-ADM-01/TestCases.md` |
| UC-ADM-02 | BR-03, BR-05, BR-08, BR-11, BR-12, BR-13, BR-14, BR-24, BR-28, BR-29, BR-35 | `Features/Admin/Menu`, Desktop `Admin/Menu`, Shared contracts/client | `tests/FourChang.Tests/QuanLy/UC-ADM-02/TestCases.md` |
| UC-ADM-03 | BR-03, BR-08, BR-14, BR-23, BR-24, BR-28, BR-29 | `Features/Admin/Ingredients`, Desktop `Admin/Ingredients`, Shared contracts/client | `tests/FourChang.Tests/QuanLy/UC-ADM-03/TestCases.md` |
| UC-ADM-04 | BR-02, BR-03, BR-08, BR-11, BR-13, BR-14, BR-23, BR-24, BR-28, BR-29 | `Features/Admin/Recipes`, Desktop `Admin/Recipes`, Shared contracts/client | `tests/FourChang.Tests/QuanLy/UC-ADM-04/TestCases.md` |
| UC-ADM-05 | BR-02, BR-03, BR-05, BR-08, BR-14, BR-23, BR-24, BR-26, BR-29, BR-35 | `Features/Admin/Inventory`, Desktop `Admin/Inventory`, Shared contracts/client | `tests/FourChang.Tests/QuanLy/UC-ADM-05/TestCases.md` |

Các quy tắc liên kết tới đơn/thanh toán/giữ-trừ kho được bảo vệ bằng snapshot/tham chiếu và nguồn catalog chung trong phạm vi năm UC. Không ghi nhận việc mở thêm giao hàng, chứng từ kho hay thay xác thực POS là đã triển khai.

## File đã thay đổi/thêm

Danh sách dưới đây bao gồm file code, cấu hình, tài liệu và test của lần triển khai; các file test xác thực Admin cũ đã chuyển từ `QuanLy/UC-ADM-02` sang `QuanLy/DungChung` vì UC-ADM-02 mới là thực đơn, không xóa nội dung kiểm thử cũ.

```text
docs/ADM01-05-IMPLEMENTATION.md
docs/database/35-bang/002-employee-contact-address.sql
docs/database/35-bang/model-before-employee-address.json
docs/database/35-bang/model.json
docs/database/35-bang/schema.sql
docs/database/37-bang/003-menu-and-outbox.sql
docs/database/37-bang/004-customer-phone-rules.sql
docs/database/37-bang/README.md
docs/database/37-bang/model-before-phone-rules.json
docs/database/37-bang/model.json
docs/database/37-bang/schema.sql
scripts/Start-LocalBackend.ps1
src/Backend/FourChang.Api/Features/Admin/Customers/CustomerAddressEndpoints.cs
src/Backend/FourChang.Api/Features/Admin/Customers/CustomerAddressService.cs
src/Backend/FourChang.Api/Features/Admin/Ingredients/IngredientEndpoints.cs
src/Backend/FourChang.Api/Features/Admin/Ingredients/IngredientService.cs
src/Backend/FourChang.Api/Features/Admin/Inventory/InventoryEndpoints.cs
src/Backend/FourChang.Api/Features/Admin/Inventory/InventoryService.cs
src/Backend/FourChang.Api/Features/Admin/Menu/CatalogReadService.cs
src/Backend/FourChang.Api/Features/Admin/Menu/MenuAdminEndpoints.cs
src/Backend/FourChang.Api/Features/Admin/Menu/MenuAdminService.cs
src/Backend/FourChang.Api/Features/Admin/Menu/MenuOutboxWorker.cs
src/Backend/FourChang.Api/Features/Admin/Menu/MenuValidation.cs
src/Backend/FourChang.Api/Features/Admin/Recipes/RecipeEndpoints.cs
src/Backend/FourChang.Api/Features/Admin/Recipes/RecipeScheduleWorker.cs
src/Backend/FourChang.Api/Features/Admin/Recipes/RecipeService.cs
src/Backend/FourChang.Api/Features/Customer/Account/CustomerSelfService.cs
src/Backend/FourChang.Api/Features/Customer/Menu/CatalogEndpoints.cs
src/Backend/FourChang.Api/Program.cs
src/Backend/FourChang.Api/Shared/Customers/CustomerRules.cs
src/Backend/FourChang.Api/Shared/Operations/AdminOperation.cs
src/Backend/FourChang.Domain/Entities/Employees/NhanVien.cs
src/Backend/FourChang.Domain/Entities/Menu/Banner.cs
src/Backend/FourChang.Domain/Entities/Menu/CauHinhMon.cs
src/Backend/FourChang.Domain/Entities/Menu/Mon.cs
src/Backend/FourChang.Domain/Entities/Menu/SuKienDongBo.cs
src/Backend/FourChang.Domain/Entities/Menu/TuyChonPhaCheHopLe.cs
src/Backend/FourChang.Infrastructure/Database/Configurations/Accounts/DiaChiKhachHangConfiguration.cs
src/Backend/FourChang.Infrastructure/Database/Configurations/Accounts/TaiKhoanKhachHangConfiguration.cs
src/Backend/FourChang.Infrastructure/Database/Configurations/Employees/NhanVienConfiguration.cs
src/Backend/FourChang.Infrastructure/Database/Configurations/Menu/BannerConfiguration.cs
src/Backend/FourChang.Infrastructure/Database/Configurations/Menu/CauHinhMonConfiguration.cs
src/Backend/FourChang.Infrastructure/Database/Configurations/Menu/MonConfiguration.cs
src/Backend/FourChang.Infrastructure/Database/Configurations/Menu/SuKienDongBoConfiguration.cs
src/Backend/FourChang.Infrastructure/Database/Configurations/Menu/TuyChonPhaCheHopLeConfiguration.cs
src/Backend/FourChang.Infrastructure/Database/Context/CafeDbContext.cs
src/Backend/FourChang.Infrastructure/Database/Initialization/CustomerSampleSeed.cs
src/Backend/FourChang.Infrastructure/Database/Initialization/DatabaseBootstrap.cs
src/Backend/FourChang.Infrastructure/Database/Initialization/DemoSeed.cs
src/Backend/FourChang.Infrastructure/Database/Validation/DatabaseChecks.cs
src/Backend/FourChang.Infrastructure/FourChang.Infrastructure.csproj
src/Presentation/FourChang.Desktop/Admin/Customers/Views/CustomerAddressBookPanel.cs
src/Presentation/FourChang.Desktop/Admin/Customers/Views/CustomerDetailsForm.cs
src/Presentation/FourChang.Desktop/Admin/Customers/Views/CustomerEditorForm.cs
src/Presentation/FourChang.Desktop/Admin/Customers/Views/CustomersPage.cs
src/Presentation/FourChang.Desktop/Admin/Ingredients/IngredientEditorForm.cs
src/Presentation/FourChang.Desktop/Admin/Ingredients/IngredientsPage.cs
src/Presentation/FourChang.Desktop/Admin/Inventory/InventoryPage.cs
src/Presentation/FourChang.Desktop/Admin/Menu/MenuEditorForm.cs
src/Presentation/FourChang.Desktop/Admin/Menu/MenuListLayout.cs
src/Presentation/FourChang.Desktop/Admin/Menu/MenuManagementPage.cs
src/Presentation/FourChang.Desktop/Admin/Menu/MenuManagementView.cs
src/Presentation/FourChang.Desktop/Admin/Recipes/RecipeEditorForm.cs
src/Presentation/FourChang.Desktop/Admin/Recipes/RecipesPage.cs
src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminListLayout.cs
src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminProfileLayout.cs
src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminUi.cs
src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminVisuals.cs
src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminWorkspacePage.cs
src/Presentation/FourChang.Desktop/Shared/Controls/CatalogImage.cs
src/Presentation/FourChang.Desktop/Shared/Controls/MetricCard.cs
src/Presentation/FourChang.Desktop/Shared/Controls/Ui.cs
src/Presentation/FourChang.Desktop/Shared/Database/DatabaseViews.cs
src/Presentation/FourChang.Desktop/Shared/Theme/CafeTheme.cs
src/Presentation/FourChang.Desktop/Shared/Theme/CafeVisuals.cs
src/Presentation/FourChang.Desktop/Staff/Sales/SalePage.cs
src/Presentation/FourChang.Web/Pages/Account/Profile.cshtml
src/Presentation/FourChang.Web/Pages/Account/Register.cshtml
src/Presentation/FourChang.Web/Pages/Menu/Index.cshtml
src/Presentation/FourChang.Web/Program.cs
src/Presentation/FourChang.Web/Services/CatalogUpdateEndpoints.cs
src/Presentation/FourChang.Web/wwwroot/js/cart-editor.js
src/Presentation/FourChang.Web/wwwroot/js/cart.js
src/Presentation/FourChang.Web/wwwroot/js/catalog-updates.js
src/Presentation/FourChang.Web/wwwroot/js/site.js
src/README.md
src/Shared/FourChang.Presentation.Shared/ApiClients/Admin/CustomerAddressClient.cs
src/Shared/FourChang.Presentation.Shared/ApiClients/Admin/InventoryClient.cs
src/Shared/FourChang.Presentation.Shared/ApiClients/Admin/MenuClient.cs
src/Shared/FourChang.Presentation.Shared/Contracts/Admin/CustomerAddressContracts.cs
src/Shared/FourChang.Presentation.Shared/Contracts/Admin/InventoryContracts.cs
src/Shared/FourChang.Presentation.Shared/Contracts/Admin/MenuContracts.cs
src/Shared/FourChang.Presentation.Shared/Contracts/Common/ScreenCatalog.cs
src/Shared/FourChang.Presentation.Shared/Contracts/Customer/CatalogContracts.cs
tests/FourChang.Tests/DungChung/NetworkFaultProxy.cs
tests/FourChang.Tests/KhachHang/UC-WEB-05/MainMenuIntegrationBrowserTests.cs
tests/FourChang.Tests/KhachHang/UC-WEB-05/MenuCustomizationTests.cs
tests/FourChang.Tests/Program.cs
tests/FourChang.Tests/QuanLy/DungChung/AccessAdministrationTests.cs
tests/FourChang.Tests/QuanLy/DungChung/AccessUiTests.cs
tests/FourChang.Tests/QuanLy/DungChung/AdminFeatureFixture.cs
tests/FourChang.Tests/QuanLy/DungChung/AdminFeaturesUiTests.cs
tests/FourChang.Tests/QuanLy/UC-ADM-01/CustomerAddressAdministrationTests.cs
tests/FourChang.Tests/QuanLy/UC-ADM-01/CustomerAdministrationTests.cs
tests/FourChang.Tests/QuanLy/UC-ADM-01/CustomerPhoneTests.cs
tests/FourChang.Tests/QuanLy/UC-ADM-01/TestCases.md
tests/FourChang.Tests/QuanLy/UC-ADM-02/MenuAdministrationTests.cs
tests/FourChang.Tests/QuanLy/UC-ADM-02/MenuSynchronizationBrowserTests.cs
tests/FourChang.Tests/QuanLy/UC-ADM-02/TestCases.md
tests/FourChang.Tests/QuanLy/UC-ADM-03/IngredientAdministrationTests.cs
tests/FourChang.Tests/QuanLy/UC-ADM-03/TestCases.md
tests/FourChang.Tests/QuanLy/UC-ADM-04/RecipeAdministrationTests.cs
tests/FourChang.Tests/QuanLy/UC-ADM-04/TestCases.md
tests/FourChang.Tests/QuanLy/UC-ADM-05/InventoryAdministrationTests.cs
tests/FourChang.Tests/QuanLy/UC-ADM-05/TestCases.md
tests/FourChang.Tests/TichHop/DatabaseTests.cs
tests/test-manifest.json
```

## Bổ sung điện thoại / cấp lại mật khẩu / Dashboard — 10/11/2026

Xem `docs/UI-CUSTOMER-DASHBOARD-REFRESH.md`. Yêu cầu mới thay thế chính sách cho phép khách chỉ dùng email: điện thoại tài khoản phải NOT NULL và đúng 10 số. Migration `005-required-customer-phone.sql` giữ 37 bảng, 73 FK, dừng khi còn khách thiếu số; không tự điền số. Form/API/Web/EF và fixture đều cập nhật cùng quy tắc.

Form Đặt lại mật khẩu nay để Admin nhập mật khẩu mới + xác nhận + lý do khi khách yêu cầu. Mật khẩu mới là tạm theo BR-07/09: thu hồi phiên cũ và khách buộc đổi lần đăng nhập tiếp theo. Không hiển thị mật khẩu cũ, không đổi chính sách bảo mật, không tạo bảng yêu cầu mới.

## Cập nhật đơn vị nguyên liệu

Theo yêu cầu bổ sung, Gam/Mililit là hai đơn vị cố định. Đã bỏ giao diện quản lý đơn vị và nhập quy đổi, đồng bộ kiểm tra API/BOM; dữ liệu/bảng và lịch sử được giữ. Phần mô tả CRUD đơn vị/quy đổi ở trên là phạm vi trước điều chỉnh. Chi tiết mới và bằng chứng kiểm thử tại docs/INGREDIENTS-FIXED-UNITS.md.
