# Báo cáo UI/UX Audit & Redesign — 04/11/2026

## 1. Thương hiệu và công nghệ được xác định
**4CHANG LINH NGU LAM**, lấy từ CafeTheme.Brand, MainForm, _Layout và logo hiện có.
Giữ nguyên brand-mark.svg, BrandMark.cs và ảnh trong assets/images; không tạo logo hoặc ảnh thay thế.
Website ASP.NET Core Razor Pages, HTML/CSS/JavaScript. Admin và POS là hai launcher WinForms
dùng FourChang.Desktop. Backend .NET8 / EF Core / SQL Server không thay đổi.
Không cài package, đổi framework, thêm csproj hoặc tạo backend mới.

Workspace chat trên C là nơi lưu tài liệu/staging; source thực của đồ án ở
`D:/2.C#C++/C #/DoAnCSharp`. Thay đổi triển khai nằm trong chính source hiện có này.
Skill ui-ux-pro-max được đọc từ `.codex/skills/.agents/skills/ui-ux-pro-max/SKILL.md`;
áp dụng thiết kế đã chốt tại design-system/4chang/MASTER.md, tra cứu bổ sung validation/error summary.
Gợi ý bulk-edit không áp dụng vì chưa có API tương ứng; không đưa thao tác giả vào bảng.

## 2. Bảng màu gốc giữ lại
| Vai trò | Sáng | Tối |
|---|---|---|
| Nâu chính / caramel | #805033 | #E4B78E |
| Espresso sidebar | #30261F | #30261F |
| Nền kem | #FAF7F2 | #201B18 |
| Bề mặt | #FFFDF9 | #2B2420 |
| Nền phụ | #F2E9DC | #3C3028 |
| Chữ chính | #302922 | #F7ECDF |
| Chữ phụ | #726454 | #C9B8A5 |
| Đường phân cách | #D9CCBC | #756150 |
| Viền ô nhập (sắc độ bổ sung) | #A28D77 | #A78F79 |

Font hệ thống Segoe UI/Tahoma/Arial hỗ trợ tiếng Việt, không tải từ mạng.
Nhịp khoảng cách4/8px; nút chính desktop44px, Web44–50px; radius8/12/16/22px theo cấp.
Shadow nhẹ ở Web; Desktop dùng viền và nền phân cấp, không giả shadow bằng nhiều Panel.

## 3. Audit: vấn đề và xử lý
| Phát hiện trước thay đổi | Phương án đã thực hiện |
|---|---|
| Nhiều form Desktop dùng ô chữ nhật, chiều cao không thống nhất | Ui.Field bọc chính input cũ trong khung native cùng palette |
| Sidebar Admin thiếu phân nhóm | Nhóm Con người & truy cập / Thực đơn & kho / Đơn hàng & tài chính, giữ đủ route |
| KPI dễ chật khi thu nhỏ | MetricCard đọc đúng giá trị caller và tự2×2 khi thiếu chỗ |
| Tổng thực thu bị vẽ như biểu đồ dù thiếu chuỗi thời gian | Bỏ lưới trang trí; chỉ hiển thị tổng thật, không nội suy theo giờ |
| Bảng read-only chưa phân biệt rỗng với đang tải | Trạng thái đang tải, lỗi và số dòng đã tải/thời điểm đọc riêng |
| Chuyển màn hình POS hủy các control nhập | Giữ hai trang sale/shift trong chính cây Form trong một phiên; hủy khi đóng shell |
| Form Web chưa có hiện/ẩn mật khẩu hoặc lỗi tại field | Thêm UI toggle giữ autocomplete, validation span và focus error summary |
| Trình tự Cart/Checkout/Payment chưa rõ | Step indicator theo đúng các trang hiện có, không đổi tính tiền |
| Sổ địa chỉ hiển thị nhiều field cùng cấp | Thẻ địa chỉ, phần vị trí tùy chọn trong details; không bỏ trường dữ liệu |
| Khách trên mobile phải tự tìm vùng tùy chỉnh sau chọn món | Cuộn tới customizer hiện có, tôn trọng reduced motion |
| Danh sách đơn Web quá thô | Empty/error state và hàng đơn từ đúng dữ liệu API |

## 4. Các màn hình Website được cải tiến
| Màn hình hiện có | Thay đổi UI | Mức nghiệp vụ hiện tại |
|---|---|---|
| Trang chủ / Menu | Hero dùng ảnh gốc, CTA tới menu, card món, bộ lọc, số kết quả thật và hết hàng | Menu đọc API; tùy chỉnh là UI hiện có |
| Đăng nhập | Nhãn, lỗi, hiện/ẩn mật khẩu, trạng thái gửi | API thật |
| Đăng ký | Nhóm thông tin2cột desktop,1cột mobile, lỗi tại field | API thật |
| Đổi mật khẩu tạm | Cùng ngôn ngữ form/toggle/error, giữ nút thử lại | API thật |
| Hồ sơ / đổi mật khẩu | Thẻ thông tin, spacing, field/validation | API thật |
| Sổ địa chỉ | Card, hành động, khu vực vị trí tùy chọn | API thật |
| Đăng xuất | Thẻ xác nhận gọn, giữ POST/antiforgery | API thật |
| Giỏ hàng | Cấu trúc dòng món, tóm tắt sticky, bước đặt món | Nội dung minh họa có sẵn, chưa bổ sung ghi giỏ |
| Checkout | Tabs có bàn phím/aria, nhãn/form, tóm tắt | Luồng nhận hàng/báo phí minh họa có sẵn |
| VietQR | Tổng tiền, chi tiết ngân hàng, trạng thái dễ đọc | QR minh họa có sẵn; không tích hợp thanh toán thật |
| Đơn của tôi | Hàng đơn, trạng thái nguyên gốc API, empty/error | API danh sách đơn thật |

Không tạo trang chi tiết món mới; customizer hiện có tiếp tục đảm nhiệm phần xem/tùy chọn.
Không tạo loading skeleton giả cho Razor render đồng bộ; phản hồi tải dùng ở nơi đang tải dữ liệu thật.

## 5. Các màn hình Staff Desktop được cải tiến
| Màn hình | Thay đổi / giới hạn được giữ |
|---|---|
| Mở ca | Nhận diện, input/frame và focus chung; mã/tên/ca vẫn được server kiểm tra |
| Shell POS | Navigation giữ đủ chức năng, bố cục co giãn, Dark Mode |
| Bán hàng | Card/nút/font chung, vùng chọn món và tóm tắt; giữ nháp khi đi qua trang khác |
| Đơn hàng | Read-only grid mới, loading/error/count từ API thật |
| Hủy món | Giữ trang đọc/tra cứu hiện có; không thêm lệnh hủy backend |
| Thu tiền & hóa đơn | Bảng hóa đơn thật trình bày mới; nút thu/in chưa hỗ trợ vẫn không giả thực thi |
| Ca làm | Khung nhập, nhóm hành động, giữ số đếm/lý do khi đổi màn hình; giữ nguyên gửi đối chiếu/chốt |

Nhân viên không có tài khoản cá nhân; vẫn dùng máy được cấp quyền và ca đã xác thực.
Không thêm sơ đồ bàn/chuyển bàn/gộp bàn khi chưa có luồng UI/API hoàn chỉnh.

## 6. Các màn hình Management Desktop được cải tiến
Dashboard thật; Khách hàng, hồ sơ/sửa/địa chỉ/trạng thái/reset/xóa có điều kiện;
Nhân viên và hồ sơ/lịch sử; Ca làm việc và đối chiếu; Quyền truy cập/tài khoản/thiết bị.
Các trang Thực đơn, Nguyên liệu, BOM, Tồn kho, Chứng từ kho, Đơn hàng, Hóa đơn,
Hoàn tiền, Báo cáo & nhật ký được hưởng bộ tab/grid/loading/count/style dùng chung,
vẫn chỉ có các thao tác dữ liệu đang được triển khai.

Dashboard hiển thị Revenue/CompletedOrders/PendingRefunds/LowIngredients đúng response cũ.
Khu vực món hiển thị là **Thực đơn đang bán**, không đổi tên thành Top món khi API chưa có số bán.
Không thêm biểu đồ thời gian, tỷ lệ hoặc dự báo từ một giá trị tổng.

## 7. Component / style chuẩn hóa và danh sách file triển khai
Ui: Label/Button/Field/Panel/Grid/Tabs/Kpis/Columns; MetricCard; EntryInputFrame;
CafeTheme/InputBorder; sidebar MainForm; DatabaseViews/Table; Dashboard/RevenueSummary.
Web giữ tokens/refresh, thêm interface.js tách xử lý UI khỏi site.js và account.js.
Dark Mode có sẵn được bảo toàn, không lưu secret hoặc thông tin khách vào localStorage.
Các tên control, handler và binding cũ được giữ; các wrapper dùng chính control instance.

| File (tương đối với đồ án) | Loại thay đổi |
|---|---|
| `src\Presentation\FourChang.Desktop\Admin\Dashboard\DashboardPage.cs` | Chỉnh sửa |
| `src\Presentation\FourChang.Desktop\Shared\Controls\Admin\AdminListLayout.cs` | Chỉnh sửa |
| `src\Presentation\FourChang.Desktop\Shared\Controls\Admin\AdminVisuals.cs` | Chỉnh sửa |
| `src\Presentation\FourChang.Desktop\Shared\Controls\Entry\EntryVisuals.cs` | Chỉnh sửa |
| `src\Presentation\FourChang.Desktop\Shared\Controls\MetricCard.cs` | Mới |
| `src\Presentation\FourChang.Desktop\Shared\Controls\Ui.cs` | Chỉnh sửa |
| `src\Presentation\FourChang.Desktop\Shared\Database\DatabaseViews.cs` | Chỉnh sửa |
| `src\Presentation\FourChang.Desktop\Shared\Theme\CafeTheme.cs` | Chỉnh sửa |
| `src\Presentation\FourChang.Desktop\Shell\MainForm.cs` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Account\ChangeTemporaryPassword.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Account\Login.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Account\Logout.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Account\Profile.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Account\Register.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Cart\Index.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Checkout\Index.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Checkout\Payment.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Menu\Index.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Orders\Index.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\Pages\Shared\_Layout.cshtml` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\wwwroot\css\refresh.css` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\wwwroot\css\tokens.css` | Chỉnh sửa |
| `src\Presentation\FourChang.Web\wwwroot\js\interface.js` | Mới |

Các file test cập nhật trong project chung:
`KhachHang/AppearanceBrowserTests.cs`, `TichHop/RedesignConnectedTests.cs`, `Program.cs`.
Không tạo project test riêng theo UC.

## 8. Kết quả build / test / bảo toàn
- Build FourChang.sln: **0 warning,0 error**.
- Toàn bộ test: **33/33 đạt**,0 thất bại,0 bỏ qua; thời gian7phút22giây.
- QA Web cuối: **1/1 đạt**, gồm Orders sau đăng nhập và trạng thái rỗng thật.
- Web: hai theme ×5viewport (320/375/768/812ngang/1440),70lượt trang trước đăng nhập,
  cùng hồ sơ và đơn hàng sau đăng nhập; không tràn ngang, kiểm tra contrast token, reduced motion,
  đổi theme/password visibility giữ nháp, bộ lọc và bàn phím tabs.
- Desktop: toàn bộ route thật Admin/POS và tab read-only, hai theme;63ảnh bằng chứng API fixture.
  Xác nhận giữ cùng TextBox/NumericUpDown và FindForm khi chuyển sale/shift.
- Hồi quy: authentication/authorization, hủy phiên, CRUD khách/địa chỉ/nhân viên, thiết bị POS,
  ca/đối chiếu/bàn giao, xung đột phiên bản, audit rollback, mất phản hồi và nâng cấp schema.
- Trong QA đã phát hiện vòng lặp native TabControl.Resize và sửa trước lần chạy đạt cuối.
- `git diff --check` không phát hiện lỗi khoảng trắng.

TRX: `artifacts/tests/brand-redesign/brand-redesign-verified.trx` và `web-final.trx`.
Ảnh thật fixture: `artifacts\tests\xunit\Redesign-Connected\FourChang_Xunit_b6fd9bcc5c77455b99482efec13bebca` và `artifacts\tests\xunit\Appearance-Web\FourChang_Xunit_5855f160eb41447cb8ee9dfb24c54236`.
Hash đối chiếu backend, domain, infrastructure, contracts/shared, csproj và logo trong
`artifacts/ui-redesign/preservation-check.json`: không thay file được bảo vệ.
Logo/ảnh gốc trong assets/images cũng được đối chiếu git diff, không có thay đổi.
Kiểm thử API/SQL dùng database FourChang_Xunit_GUID riêng; không reset dữ liệu quán.

## 9. Vấn đề còn tồn tại / giới hạn
- WinForms là Windows desktop, không chạy trực tiếp trên trình duyệt điện thoại. Web responsive riêng.
- Các UC chưa triển khai nghiệp vụ hoàn chỉnh vẫn giữ mức hỗ trợ trước redesign; báo cáo trên phân biệt rõ.
- Chưa có API chuỗi doanh thu theo thời gian, top món theo bán thực tế hoặc bản đồ/báo phí thật;
  không bổ sung backend và không tự tạo số liệu.
- QR/chuyển khoản/thu tiền/in trong các màn hình minh họa không trở thành chức năng thật sau redesign.
- Các control native như lịch và một phần mũi tên ComboBox vẫn theo Windows; không đổi framework để tô vẽ chúng.
- Bảng có nhiều cột kỹ thuật vẫn cho cuộn ngang; không giấu/xóa dữ liệu API để giả vừa màn hình.
- Chạy lại Web tại http://localhost:15180;15181 là API và không phải trang chủ Website.


## Ảnh kiểm tra
Các ảnh sau được render từ ứng dụng thật với database kiểm thử riêng, không phải số liệu quán:
- [Dashboard sáng](<D:/2.C#C++/C #/DoAnCSharp/artifacts/tests/xunit/Redesign-Connected/FourChang_Xunit_b6fd9bcc5c77455b99482efec13bebca/Admin-dashboard-light.png>)
- [Nhân viên tối](<D:/2.C#C++/C #/DoAnCSharp/artifacts/tests/xunit/Redesign-Connected/FourChang_Xunit_b6fd9bcc5c77455b99482efec13bebca/Admin-employees-dark.png>)
- [POS sáng](<D:/2.C#C++/C #/DoAnCSharp/artifacts/tests/xunit/Redesign-Connected/FourChang_Xunit_b6fd9bcc5c77455b99482efec13bebca/Staff-sale-light.png>)
- [Web sáng](<D:/2.C#C++/C #/DoAnCSharp/artifacts/tests/xunit/Appearance-Web/FourChang_Xunit_5855f160eb41447cb8ee9dfb24c54236/menu-light-1440.png>)
- [Đơn hàng đăng nhập mobile](<D:/2.C#C++/C #/DoAnCSharp/artifacts/tests/xunit/Appearance-Web/FourChang_Xunit_5855f160eb41447cb8ee9dfb24c54236/orders-authenticated-mobile.png>)

## Sửa lỗi tài nguyên khi chạy DLL (04/11/2026)
Program.cs xác định WebRoot bằng thư mục wwwroot cạnh DLL; Web.csproj copy tài nguyên cùng bản build. Sửa lỗi CSS/JS/logo 404 khi mở từ thư mục gốc ở Production. Không thay API/schema/nghiệp vụ. StaticAssetsHostingTests: 1/1 đạt, xác minh 12 tài nguyên và render Chromium. Web thật trên cổng 15180 đã kiểm tra CSS/JS/logo/ảnh đều HTTP 200.

