# UI Refinement — 05/11/2026

Tinh chỉnh trực tiếp trên giao diện hiện có bằng skill ui-ux-pro-max.
Giữ **4CHANG LINH NGU LAM**, logo, ảnh, sidebar, header, các vùng chức năng, thứ tự navigation
và toàn bộ luồng nghiệp vụ hiện tại. Không chạy bản đề xuất đổi nền/khung layout trước đó.
Source thực: `D:/2.C#C++/C #/DoAnCSharp`.

## 1. Alignment và spacing đã khắc phục
| Vấn đề | Tinh chỉnh |
|---|---|
| Nút trong nhóm có chiều cao42/44px, margin khác nhau | Chuẩn hóa nút thao tác44px và khoảng cách8px; giữ độ rộng theo caption |
| Khung input Desktop44/50px không đồng đều | Khung chuẩn48px; nhãn24px trong bộ lọc, căn control native theo chiều dọc |
| Icon/chữ nút Admin lệch khi nhấn | Căn giữa cả cụm icon+text, press chỉ đổi màu, không dịch chữ xuống1px |
| Nút + POS bị Dock kéo thành bầu dục | Kích thước44×44, Anchor.None, căn giữa cell; cùng handler chọn món cũ |
| Nhóm ca/tiền đầu ca có nguy cơ xuống dòng | Tinh chỉnh mỗi field205px và gutter16px để giữ cùng hàng |
| Header dư margin sau bỏ nút theme | Chuẩn hóa margin nội bộ, giữ chiều cao header66px |
| Sidebar có scrollbar ngang ở cửa sổ thấp | Bù scrollbar native, giới hạn control trong vùng client, nhãn ngắt đúng cụm từ |
| Caption tab bị rút ngắn | Tab native lấy độ rộng theo chữ; vẫn có mũi tên cuộn và tooltip |
| Quantity, segmented, action row Web chưa cùng baseline | Inline-flex căn giữa, gutter8px, line-height1.4, output số có chiều rộng ổn định |
| Divider và trạng thái disabled không thống nhất | Token line mới và viền disabled dịu; hover/focus/selected có phân cấp |

Các chỉnh sửa sidebar/tab chỉ là fitting cục bộ, không đổi độ rộng sidebar Admin212px/POS108px,
không chuyển nhóm chức năng sang vùng khác, không thêm route hoặc sắp lại menu.

## 2. Bảng màu tinh chỉnh duy nhất
| Vai trò | Trước | Sau |
|---|---|---|
| Nâu chủ đạo | #805033 | #805033 — giữ nguyên |
| Espresso sidebar | #30261F | #3A2E25 — dịu và sáng hơn |
| Nền kem | #FAF7F2 | #FBF8F2 |
| Bề mặt card | #FFFDF9 | #FFFDF9 — giữ nguyên |
| Nền phụ | #F2E9DC | #F4ECE0 |
| Divider | #D9CCBC | #E1D5C5 |
| Chữ phụ | #726454 | #716252 |
| Chữ chính | #302922 | #302922 — giữ nguyên |
| Viền control | #A28D77 | #A28D77 — giữ độ rõ |

Nền Web chỉ có một chuyển sắc kem rất nhẹ ở đầu trang, không thêm họa tiết, frame mới,
glassmorphism hoặc gradient lặp. Nội dung và card vẫn phân biệt bằng bề mặt/viền.

## 3. Thành phần Light/Dark đã loại bỏ
- Xóa nút/menu chuyển màu trên Web, MainForm và EntryForm.
- Xóa `wwwroot/js/appearance.js` và thẻ script nạp nó.
- Xóa CSS dark selector, icon mặt trời/mặt trăng và style nút theme.
- Xóa `CafeAppearance.cs`: IsDark, SetDark, Toggle, Changed, đọc/ghi JSON và ánh xạ đảo màu.
- Thêm `CafeVisuals.cs` chỉ chuẩn hóa control native, không có trạng thái hoặc persistence theme.
- Giữ `CafeTheme.cs` và tokens.css như bảng màu chung cố định; tên CafeTheme không còn đại diện
  chức năng lựa chọn theme. Token Dark chỉ là màu espresso sidebar.
- Không đọc, ghi, xóa cookie/localStorage hoặc cấu hình theme cũ. Giá trị cũ không thể ghi đè màu.
  Các cài đặt người dùng khác không bị đụng tới.
- Không theo màu hệ điều hành. Quyền giảm animation/tương phản cao của hệ điều hành vẫn được tôn trọng.

## 4. Animation và trạng thái tương tác
- Web hover/focus khoảng180ms, vào nội dung220ms, mở dialog180ms.
- Hiệu ứng card dịch nhẹ và button press nhỏ đã có được giữ, tôn trọng prefers-reduced-motion.
- Navigation active indicator, secondary/danger hover và disabled được đồng bộ.
- Desktop hover khoảng160ms, press đổi màu tức thì và không đổi bounds/icon/text.
- Giữ loading strip chỉ chạy khi có tác vụ; timer dừng/dispose khi hoàn tất/rời control.
- Không thêm timer animation cho sidebar, không tự vẽ lại popup lịch/dropdown native.

## 5. File giao diện đã thay đổi
Đường dẫn dưới đây tương đối với source đồ án:

| File | Thay đổi |
|---|---|
| `src/Presentation/FourChang.Desktop/Admin/Dashboard/DashboardPage.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/DesktopLauncher.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminListLayout.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminVisuals.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Shared/Controls/Entry/EntryVisuals.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Shared/Controls/NavigationButton.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Shared/Controls/Ui.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Shared/Database/DatabaseViews.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Shared/Theme/CafeAppearance.cs` | Đã loại bỏ |
| `src/Presentation/FourChang.Desktop/Shared/Theme/CafeTheme.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Shared/Theme/CafeVisuals.cs` | Thêm helper style cố định |
| `src/Presentation/FourChang.Desktop/Shell/EntryForm.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Shell/MainForm.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Staff/Sales/SalePage.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Desktop/Staff/Sales/SaleView.cs` | Tinh chỉnh |
| `src/Presentation/FourChang.Web/Pages/Shared/_Layout.cshtml` | Tinh chỉnh |
| `src/Presentation/FourChang.Web/wwwroot/css/refresh.css` | Tinh chỉnh |
| `src/Presentation/FourChang.Web/wwwroot/css/tokens.css` | Tinh chỉnh |
| `src/Presentation/FourChang.Web/wwwroot/js/appearance.js` | Đã loại bỏ |

Test C# dùng cùng project hiện có:
- `tests/FourChang.Tests/KhachHang/AppearanceBrowserTests.cs`
- `tests/FourChang.Tests/TichHop/AppearanceDesktopTests.cs`
- `tests/FourChang.Tests/TichHop/RedesignConnectedTests.cs`
- `tests/FourChang.Tests/TichHop/StaticAssetsHostingTests.cs`

## 6. Build, kiểm thử và quan sát trực quan
- Build solution cuối: **0 lỗi,0 cảnh báo**.
- Bộ UI/hồi quy cuối: **10/10 đạt**,0 thất bại,0 bỏ qua.
- Web kiểm tra5viewport320/375/768/812ngang/1440, hệ điều hành sáng và tối đều dùng cùng palette;
  localStorage theme cũ="dark" bị bỏ qua mà không bị xóa. Kiểm tra contrast, không tràn ngang,
  nhập liệu, password toggle, menu, checkout tabs, profile và Orders sau đăng nhập.
- Desktop kiểm tra toàn bộ route ở1440×960 và1100×740; nút cùng hàng cùng chiều cao,
  sidebar không tràn ngang, không còn nút theme, JSON theme cũ không bị thay đổi.
- Worker kết nối API/SQL test riêng kiểm tra mọi route thật Admin/POS và các tab dữ liệu;
  giữ nháp tìm món/số đếm, owner/FindForm, và nút + đúng44×44.
- Các test hiện có kiểm tra thêm form khách/nhân viên, địa chỉ, thiết bị, mở ca,
  phiên đăng nhập, receipt, xác thực, mất mạng và ràng buộc quyền.
- Đã render và xem ảnh từng nhóm màn hình, tab dữ liệu, form/modal, hai cỡ cửa sổ.
  Lỗi nút + méo và scrollbar ngang được sửa từ ảnh/bounds thực tế, không kết luận chỉ từ build.
- Protected hash: **148 file**,0 thay đổi; backend, domain, shared/contracts, schema,
  ảnh và logo giữ nguyên.35bảng nghiệp vụ và26UC không thay đổi.

TRX: `artifacts/tests/ui-refinement/ui-refinement-delivery.trx`.
Proof: `artifacts/ui-refinement/verification.json`.
Ảnh route thật: `artifacts\tests\xunit\Redesign-Connected\FourChang_Xunit_ef2980dc79494406917e4a6d3372b27d`.
Ảnh kích thước cửa sổ: `artifacts\tests\ui-refinement-desktop\7057de04dcd2471bbaa846c27b454c5d`.
Ảnh Web: `artifacts\tests\xunit\Appearance-Web\FourChang_Xunit_cc919f6be0234ac29d9944fbd05c2847`.

## 7. Phạm vi giữ nguyên và giới hạn
- Các thao tác chưa có backend trước đây vẫn giữ mức hỗ trợ cũ; không thêm chức năng giả.
- Không đổi authentication, authorization, giá, phí giao, thanh toán, kho, SQL hay API contract.
- Bảng nhiều cột vẫn cuộn ngang trong vùng bảng; không giấu/xóa cột dữ liệu.
- Text email/ID dài trong ô nhập vẫn xem/sửa bằng con trỏ; trường read-only dài có tooltip.
- WinForms vẫn là Windows desktop; Web vẫn responsive riêng.
