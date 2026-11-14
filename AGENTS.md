# Ghi nhớ dự án 4CHANG

Cập nhật: 13/11/2026. Đây là ghi nhớ và hướng dẫn cho agent; code và git diff hiện tại là nguồn xác nhận cuối cùng.

## Cách làm việc để giảm đọc lặp và token
- Đọc file này trước; chỉ mở file của chức năng đang sửa và các phụ thuộc trực tiếp.
- Kiểm tra git status, giữ các thay đổi của nhóm/người dùng; không reset/revert hoặc push nếu chưa được yêu cầu.
- Gom chỉnh sửa rồi build/test phù hợp một lượt. Chỉ chạy lại khi có lỗi, thay đổi mới hoặc vấn đề còn chưa xác minh.
- Với UI, kiểm tra ảnh/giao diện thực; không kết luận đẹp hoặc hết chớp chỉ dựa vào build.
- Trả lời ngắn, hạn chế cập nhật lặp. Chỉ hỏi khi còn quyết định nghiệp vụ cần người dùng chốt.
- Sau công việc, cập nhật mục gần nhất bên dưới: file, quyết định, kiểm thử và vướng mắc. Không viết nhật ký dài hoặc lặp lại toàn bộ audit.
- Không ghi mật khẩu, token, chuỗi kết nối hay nội dung .env vào đây/log trả người dùng.

## Repo và công nghệ
- Repo đang làm trên máy này: D:\2.C#C++\C #\DoAnCSharp. Thư mục Codex C:\Users\gauba\OneDrive\Documents\ChatGPT\C# còn chứa nhiều bản sao/công cụ cũ; không nhầm repo.
- .NET 8, C#, EF Core/SQL Server; Web Razor Pages; Desktop WinForms. Không đổi framework/tạo project mới.
- src/Backend/FourChang.Api: API, nghiệp vụ; FourChang.Domain: entity; FourChang.Infrastructure: EF/schema/seed.
- src/Presentation/FourChang.Desktop: code WinForms dùng chung; FourChang.Admin/FourChang.Pos là entry app.
- src/Presentation/FourChang.Web: Razor, wwwroot/css, wwwroot/js.
- src/Shared/FourChang.Presentation.Shared: DTO và API clients.
- tests/FourChang.Tests: xUnit, SQL/API thật, WinForms worker STA, Playwright. Các test dùng DB GUID riêng.
- Không xóa bảng/database/volume để sửa giao diện. Các thay đổi schema đã chốt nằm trong docs/database/37-bang.

## Chạy local
- API http://localhost:15881; Web http://localhost:15880/ (Menu có @page "/", /Menu không phải route trang chủ).
- Docker SQL: fourchang-sqlserver-1, cổng 14330. Kiểm tra docker ps; nếu dừng có thể start container hiện hữu, đợi healthy rồi chạy test.
- scripts/Start-LocalBackend.ps1; scripts/Start-LocalWeb.ps1. Chay-QuanLy.cmd/Chay-NhanVien.cmd build lại entry app khi có SDK.
- .env ngoài Git, chứa cấu hình local; không in nội dung ra.
- Nếu DLL bị giữ khi build, chỉ dừng process Web/API thuộc repo đang nghe đúng cổng. Không tắt cưỡng bức Desktop người dùng.
- Có sửa DLL Desktop thì người dùng cần đóng/mở lại app. Không kết luận đã đồng bộ GitHub nếu chưa fetch/push theo yêu cầu.

## Các quy tắc đã chốt với người dùng
- Thương hiệu 4CHANG LINH NGU LAM; giữ logo, nâu/kem, bố cục chính. Một theme duy nhất, không Light/Dark switch.
- Số điện thoại khách đúng 10 chữ số; mật khẩu tối thiểu 5 ký tự. Dữ liệu mẫu dùng tên tự nhiên, thông tin giả lập.
- Giá món/cấu hình/topping lấy từ API/database dùng chung, không khai báo giá riêng ở từng giao diện.
- Tắt bán vẫn hiện Tạm hết; chỉ ẩn khi chủ động ẩn/công bố. Món nóng không hiện % đá; size, nhiệt độ, đường/đá chọn riêng theo cấu hình hợp lệ.
- Website mô phỏng giao hàng 15.000đ/đơn, chọn bàn từ danh sách. QR thanh toán mô phỏng được cho phép cho đồ án; không khẳng định là xác minh ngân hàng thật.
- POS không đăng nhập tài khoản cá nhân từng nhân viên; mở ca xác định nhân viên. Mỗi nhân viên được mở Sáng/Chiều/Tối mỗi loại một lần trong ngày UTC+07; không bắt buộc giờ thực tế, không khóa máy hoặc chặn ca khác tên. Ngày mới được chọn lại. Kết thúc ca lưu đối chiếu, kết thúc và về mở ca. Đã bỏ tạm khóa ca/đề nghị tiếp tục, gồm UI và endpoint; vẫn giữ phục hồi chỉ đối chiếu cho sự cố/mất phiên.
- Khách yêu cầu hủy -> Nhân viên gửi Quản lý -> Quản lý duyệt/từ chối; duyệt mới hủy. Khách đến quầy nhận hoàn, chi thực tế theo hạn mức, không coi duyệt là đã chi.
- Đơn bình thường và Đơn hủy tách riêng. Staff Đơn hủy chỉ có Gửi yêu cầu duyệt; chi hoàn ở Hóa đơn -> Hoàn tiền tại quầy.
- Tiếp nhận/bắt đầu pha/pha xong/hoàn tất không nhập lý do thủ công; server tự ghi nhật ký. Hủy/ngoại lệ vẫn có lý do/chứng cứ.
- Số ly thao tác mặc định là phần còn lại của dòng món: bắt đầu = đặt-hủy-đã bắt đầu; pha xong = đã bắt đầu-đã xong. Không trừ kho lại khi replay/pha xong.
- Mã đơn ngắn WEB-ID/POS-ID; mã cũ/snapshot chứng từ không bị viết lại.

## Bản đồ các màn hình vừa làm
- Shared/Orders/OrdersWorkspacePage.cs và partial OrderRefresh.cs: danh sách/chi tiết/tiến trình đơn, dùng chung Admin+Staff. API OrderAdministrationService; lệnh OrderWorkflowService.
- Shared/Orders/OrderDetailsForm.cs: chỉ 4 phần Thông tin đơn / khách / món / thanh toán. Không hiện kho, tiêu hao, nhật ký kỹ thuật.
- Địa chỉ chi tiết đơn: giao hàng dùng DiaChi (địa chỉ giao đã chốt); đơn khác dùng DiaChiKhachHang từ sổ địa chỉ đang dùng, ưu tiên mặc định. Không có thì Chưa cung cấp. Không đổi địa chỉ giao theo thay đổi hồ sơ.
- Shared/Invoices/InvoiceDetailsForm.cs: chỉ 5 phần Quán / hóa đơn / khách / món / thanh toán. Logo/tên thật; tên khách ưu tiên hóa đơn rồi người nhận, thiếu thì Khách lẻ. Điện thoại/địa chỉ từ đơn; địa chỉ hóa đơn hiện khi giao hàng.
- InvoiceSnapshotReader trong file trên đọc lines/toppings đã phát hành, không lấy giá menu. Snapshot cũ thiếu dòng món báo thiếu dữ liệu, không dựng giả. Chức năng in/PDF Quản lý giữ nguyên.
- Staff/Invoices/StaffInvoicesPage.cs (tab Hóa đơn POS) và Admin/Invoices/InvoicesManagementPage.cs mở cùng InvoiceDetailsForm. StaffDocumentsPage.cs là trang bảng thô cũ, không dùng cho tab hóa đơn chính nữa.
- Staff/Sales/PosSalesPage.cs: giỏ nhiều món, polling menu 5 giây; chỉ dựng lại thẻ khi dữ liệu đổi. PosItemEditorForm.cs tách size/nóng/lạnh/đường/đá, kiểm tra lại API trước lưu.
- Polling đơn so sánh snapshot, giữ DataSource/chọn/cuộn khi không đổi; không bỏ polling để che lỗi chớp.
- Đã bỏ nhãn Cập nhật/Mốc dữ liệu tải trên Desktop/Web; vẫn giữ ngày nghiệp vụ thật, loading/lỗi và polling. Test polling dùng Grid.Tag/PosProductGrid.Tag, không phụ thuộc nhãn giờ.

## Kiểm thử và bằng chứng gần nhất
- Build: dotnet build tests/FourChang.Tests/FourChang.Tests.csproj --no-restore -v minimal
- Chạy lọc: dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --no-restore --filter "FullyQualifiedName~TenLopTest" --logger "trx;LogFileName=ten.trx" --results-directory artifacts/test-results
- WinForms: tests/.../QuanLy/DungChung/PosSalesUiChecks.cs và StaffDocumentsUiChecks.cs; Program.cs đăng ký worker, DesktopWorker chạy process STA. Capture và WaitUntil dùng AdminFeaturesUiTests.
- Chỉ chạy invoice UI hiện tại bằng DisplayName~staff-invoices; case staff-orders cũ trong StaffDocumentsUiTests dùng layout cũ, cần cập nhật riêng nếu đụng tới.
- 8 test hóa đơn đạt: basic-invoice-details.trx; 5 test bổ sung khách/địa chỉ UI đạt: basic-invoice-customer-ui.trx.
- 11 test bỏ nhãn cập nhật đạt: remove-refresh-labels.trx. 7 test chi tiết đơn cơ bản đạt: basic-order-details.trx.
- Ảnh thực: artifacts/tests/xunit/ADM01-05/<DB test>/; TRX artifacts/test-results/. Không xem ảnh cũ rồi khẳng định là bản mới: chọn theo LastWriteTime.
- Báo cáo liên quan: docs/ORDER-DETAILS-BASIC.md, INVOICE-BASIC-VIEW.md, POS-PREPARATION-REFINEMENT.md, SHORT-ORDER-CODES.md, STAFF-ORDER-WORKFLOW.md.

## Công việc gần nhất
- Thêm địa chỉ hồ sơ vào chi tiết đơn tại bàn/mang đi; giữ nguyên địa chỉ giao đã chốt. API chỉ bổ sung trường đọc tùy chọn, không migration hay sửa dữ liệu khách thật.
- Tạo file AGENTS.md này làm bộ nhớ dự án tự đọc ở các lượt sau.
- Kiểm thử sửa địa chỉ: 6/6 test đạt (order-customer-address.trx), gồm API tách địa chỉ hồ sơ/địa chỉ giao và UI chụp hai trường hợp. Build không lỗi; backend/Web đã chạy lại.

- Yêu cầu mới: Bán hàng bỏ Khách đưa và tiền thừa, chỉ hiện tổng tiền; khi bấm thu CASH gửi đúng tổng làm tiền nhận. API vẫn kiểm giá/thu/receipt như cũ, không tự thu khi chỉ xem giỏ. Bỏ lý do tiếp nhận ở cả POS/Admin, audit tự ghi Tiếp nhận đơn hàng. Kiểm thử: 5/5 test đạt (accept-exact-cash.trx), có ảnh POS-multi-item-cart.png và accept-without-reason.png. Build không lỗi; API/Web đã chạy lại.

- Hóa đơn POS mới: StaffInvoicesPage.cs có tìm số hóa đơn/mã đơn/tên khách, Từ ngày–Đến ngày (bao gồm hết ngày cuối), Enter/Tìm kiếm, Tải lại, phân trang 50 dòng. API đọc /api/pos/invoices và /{id} chỉ quyền Staff; dùng InvoiceAdministrationService lọc trước Count/Skip/Take, không giới hạn 100 bản ghi. Mã đơn hiển thị WEB/POS ngắn; mặc định xem 30 ngày. Không đổi tính tiền, chứng từ hay schema. Test staff-invoice-search.trx: 5/5 đạt, gồm 105 hóa đơn, cận ngày, tên/mã đơn, phân quyền và UI mở chi tiết; ảnh staff-invoices.png. Cần mở lại POS sau build.

- Ca làm: không có tạm khóa/tiếp tục hoặc ô lý do. Tiền mặt cuối ca phải khớp đầu ca + CASH VERIFIED của chính ca - hoàn CASH SUCCEEDED của chính ca. Submit/Close/Admin chốt-bàn giao đều kiểm ở server, lệch trả 409 và không ghi tiền/trạng thái. UI hiện tiền hệ thống, thuần tiền mặt từ đơn, CK riêng, chênh lệch; chỉ cho gửi khi khớp. TienThuDuoc hiện tính từ giao dịch (không lấy số nhân viên nhập làm doanh thu). Không migration, không sửa lịch sử cũ.

- Tách đơn ca trên POS: OrderShiftGroup có Ca này/đơn Web chờ nhận, Đơn nhận từ ca trước, Tất cả ca; bảng có NguonCa. PhamViCa CURRENT/PREVIOUS/ALL lọc trước Count/Skip/Take, ca hiện tại lấy từ token Staff. HandoverLink.ForReceiver chỉ đọc MaDonBanGiao trong biên bản trực tiếp của ca nhận, không lôi tất cả đơn tổ tiên. Web được Staff ACCEPT sẽ gắn MaCaLamViec của ca nhận; giao dịch thu tiền giữ nguyên ca nguồn, không thu/cộng lại. Tổng hoàn thành trên chi tiết ca đã bổ sung theo ORDER_COMPLETE audit (xem mục mới nhất); không có cột MaCaHoanThanh. Bộ lọc ca của ReportAdministrationService chưa đổi sang ca hoàn thành. Test shift-reconcile-order-groups.trx 14/14 đạt, test UI chuyển nhóm shift-order-groups-ui.trx đạt. Có một lần UI chờ ở thao tác pha chưa tái hiện ở lần kiểm tra tiếp; đã thêm ảnh/feedback lỗi trong helper để truy vết nếu lặp lại. Ảnh POS-orders-current-shift.png, POS-orders-previous-shift.png, UC-ADM-12-pos-reconciliation.png. Backend/Web đã chạy lại.

- Màn hình ca làm và chi tiết ca Admin: thêm SoDonHoanThanh/TongTienDonHoanThanh. WorkSessionService lấy đơn COMPLETED, gán ca hoàn thành từ audit ORDER_COMPLETE.MaCaLamViec; dữ liệu cũ/Admin không có ca Staff trong audit dùng ca gắn đơn làm fallback. Tổng dùng OrderFinancials.NghiaVu (giá đã chốt + phí giao, điều chỉnh theo nguồn), không cộng đơn dở/hủy; không cộng doanh số này vào tiền mặt phải đếm. Không sửa ca nguồn thanh toán/bàn giao. Test completed-shift-total.trx 11/11 đạt, gồm đơn ca khác hoàn tất không tính đôi và UI hiển thị tổng; ảnh POS-shift-completed-total.png.

- Sửa yêu cầu hủy do Staff tạo: /api/pos/orders/{id}/actions CANCEL chỉ lưu HuyMon REQUESTED, hiển thị FORWARDED (Chờ Quản lý duyệt); chưa đổi đơn, số ly hủy, giữ chỗ hay tiền. Đơn chuyển sang nhóm CANCELLATION và chặn xử lý tiếp cho đến khi Admin APPROVE/REJECT. Duyệt dùng cùng tính giá/kho của OrderWorkflowService.Cancel, gắn đúng HuyMon gốc, lưu rồi duyệt hạn mức CASH; không ghi lần chi tiền. Từ chối giữ nguyên đơn/kho/tiền. Khách yêu cầu vẫn qua bước Staff FORWARD như cũ. Không gọi receipt 200 là đã hủy; UI thông báo đã gửi/chờ duyệt.
- Màn hình ca Staff: thống kê chỉ còn tổng tiền đơn COMPLETED và số đơn COMPLETED; bỏ Tiền mặt thuần từ đơn và dòng chi tiết đầu ca/thu/hoàn/CK. Vẫn giữ số tiền cuối ca cần đếm, ô nhập/chênh lệch và điều kiện khớp để kết ca theo yêu cầu đã chốt trước. Không đổi công thức quỹ hoặc cộng doanh số hoàn thành vào tiền mặt.

- Kiểm thử thay đổi hủy chờ duyệt/tổng kết ca: 15 test liên quan; lượt đầu 13 đạt, phát hiện bước hồ sơ hoàn mới chưa Save trước query duyệt (đã sửa trong transaction), UI pha có một lần bỏ lỡ dialog như ghi chú trước. Chạy lại CancellationApprovalTests và PosSalesUiTests: 5/5 đạt (staff-cancel-summary-recheck.trx); nhóm còn lại đạt trong staff-cancel-pending-summary.trx. Ảnh mới POS-shift-completed-total.png xác nhận chỉ còn hai thống kê hoàn thành và khối kiểm đếm cuối ca. Build không cảnh báo/lỗi.

- Giá niêm yết/config: MenuProduct.Id là MaCauHinhMon của giá trên thẻ. PosItemEditorForm trước đây tự chọn S/nhiệt độ đầu danh sách (Bạc xỉu S nóng 34.000 trong khi thẻ M lạnh 39.000); nay thêm mới chọn đúng item.Id, sửa giỏ giữ variant cũ. Web cart-editor chọn product.variantId từ /Cart?handler=Catalog thay vì bỏ trống/tự lấy cấu hình đầu. API menu và catalog-updates cùng ưu tiên mặc định còn hàng, sau đó cấu hình còn hàng; hết toàn bộ thì giữ mặc định và khóa mua. Không sửa giá database hay giá snapshot đơn cũ. Form/giỏ/thu tiền vẫn dùng GiaBan của variant + topping nhân số ly. Test UI CheckListedPrices duyệt mọi món khả dụng, kiểm size/nhiệt độ/giá và 2 ly; có ảnh POS-listed-price-bac-xiu.png (M lạnh, 2 ly 78.000).

- Kiểm thử giá: 21 test liên quan đã đạt qua các lượt listed-variant-prices.trx (18 đạt; test mới ban đầu đặt assertion trước khi chọn đường), listed-price-recheck.trx (2 API/Web đồng bộ đạt), listed-default-browser.trx (test mặc định Web sửa thứ tự assertion đạt). Bao gồm mọi món POS mặc định, nhiều ly, thanh toán CASH/QR, giỏ Web và cập nhật giá/còn hàng thật. Không sửa dữ liệu kinh doanh.

- Quy tắc mới thay phần cho lặp tên ca: WorkEndpoints.UsedShifts dùng [00:00,00:00 ngày sau) UTC+07, lấy cả OPEN/LOCKED/CLOSED. Open kiểm sau receipt dưới transaction Serializable + AdminOperation.Lock, chống hai máy/mã mới chọn cùng ca. Gửi lại cùng mã vẫn trả đúng ca đã mở. Không sửa ca/đơn/tiền lịch sử hay schema. VerifyEmployee trả CaDaLamTrongNgay (Employee optional field); EntryForm.ShowIdentity chỉ liệt kê tên ca còn lại, kiểm lại trước mở và báo đủ 3 ca. Sáng -> Tối -> Chiều vẫn hợp lệ, không bắt thứ tự giờ.

- Kiểm thử quy tắc ca mỗi ngày: daily-distinct-shifts.trx 6/6 đạt (ba ca khác tên, ca đã đóng, máy khác, cạnh tranh hai yêu cầu, replay, ngày UTC+07 mới, danh tính và kết ca); daily-shift-picker-ui.trx đạt sau bổ sung assert dropdown chỉ còn Chiều/Tối với nhân viên đã làm Sáng, nhân viên khác vẫn đủ 3 ca. Ảnh staff-remaining-shifts.png. Build không lỗi.

- Đối chiếu 13 UC Khách/Nhân viên và BR gốc: docs/CUSTOMER-STAFF-CHANGES.md (13/11/2026). GitHub main = HEAD 7431df1 nhưng working tree chưa commit/push; nhánh feat/thai-web-cart-order phân kỳ 8 ahead/3 behind. Phát hiện Register.cshtml.cs và ChangeTemporaryPassword.cshtml.cs còn MinimumLength=10 dù yêu cầu/HTML là 5; chưa sửa trong lượt tổng hợp. SHIFT-DAILY-RULE.md và POS-WEB-PAYMENT-DELIVERY.md có đoạn cũ đã bị thay thế. Báo cáo nêu giới hạn QR mô phỏng, địa chỉ hóa đơn và hủy một phần/chi quầy; không khẳng định tất cả hoàn tất hoặc đã đồng bộ.
