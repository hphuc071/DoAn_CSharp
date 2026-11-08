# UC-WEB-07 — Thanh toán chuyển khoản: phạm vi offline/mock

## Kết luận audit hiện hành — 2026-11-08

Nền code đã có SQL writer/payment planning và bằng chứng direct-service SQL lịch sử; không có
provider ngân hàng/QR thật, callback công khai hoặc thanh toán production được bật. Các mô tả
Phase bên dưới là lịch sử, không thay thế kết luận audit tại
`artifacts/final-review/UC07-COMPLETION-AUDIT.md`.

Audit bổ sung các sửa lỗi có regression: từ chối snapshot có tổng/số dư tràn decimal trước render;
không gọi QR source cho đơn không còn đủ điều kiện thanh toán; QR read có timeout5s/cancellation
và xử lý lỗi JSON/content-type mà không lộ thông tin nguồn; chỉ nhận deadline QR UTC explicit;
browser ẩn QR đến hạn mà không đổi trạng thái, chuyển tiền hoặc xóa pending Checkout.
PaymentVerificationPolicy dùng cùng validation evidence với SQL writer để tránh chấp nhận shape
không hợp lệ ở planner. Auth/receiver denial phát security event7001 chỉ chứa code cố định,
không raw payload/headers/account. Không thay transaction, SQL context/schema hoặc hợp đồng UC06/UC08.

**Blocker cụ thể:** Create UC06 hiện tạo payment intent nhưng chưa cấp reference/QR; chưa có
binding receiver/provider revision bất biến cho từng intent. Không tự invent reference/beneficiary
để mở QR. Quy tắc delayed callback production, BR17 và rollout đối soát/hoàn vẫn cần quyết định.
Hợp đồng bàn giao và điều kiện bật từng capability ở
[`UC-WEB-07-PROVIDER-HANDOFF.md`](UC-WEB-07-PROVIDER-HANDOFF.md). Hai cờ
`Orders:PaymentProcessingEnabled` và `Orders:SqlIntegrationValidated` không được bật bởi audit.

## Hồ sơ triển khai lịch sử

> Cập nhật tổng review 2026-11-07: SQL fixture V2 đã PASS 26 scenario records cho payment/cancel trực tiếp trên SQL. Các mục Phase cũ giữ lịch sử, không phải kết luận hiện hành. Chưa nghiệm thu provider, HTTP thanh toán, expiry hoặc PDF. Xem `artifacts/uc-web-07-08/SQL-INTEGRATION-RESULT.md` và `artifacts/final-review/UC-WEB-06-07-08-READINESS.md`.



**Cập nhật Phase 4:** đã có SqlBankPaymentService và SqlOrderExpiryWorker để tích hợp sau này, mặc định khóa và chưa kiểm thử ghi SQL. Những mô tả Phase1/2 bên dưới là lịch sử các gói trước; trạng thái mới nhất, flags, migration review-only và giới hạn actor/refund xem `artifacts/uc-web-07-08/RESULT-PHASE4.md`. Không có bank provider/callback route thật được bật.

Đối chiếu đặc tả `dac-25-Uc.docx`, mục UC-WEB-07 và BR-05/08/12/14/16/17/18/21/29/36. Tài liệu đặc tả là đầu vào nghiệp vụ; các bước ghi tiền trong đó không phải quyền thực hiện giao dịch ở lượt này.

## Hiện trạng và quyết định

Trước thay đổi, `/Checkout/Payment` chỉ có đơn/tiền/ngân hàng/thời gian mẫu và nút thay trạng thái minh họa. Backend đã có `ThanhToan`, `GiaoDichNganHang`, `HoSoHoanTien`, `LanChiHoanTien`, `HoaDon`; không tạo lại bảng, context hoặc payment request. Khóa duy nhất của giao dịch ngân hàng đã là `(TenNhaCungCap, MaTaiKhoanNganHangQuan, MaGiaoDichBenNgoai)`.

Trang mới đọc snapshot đơn WEB có kiểm tra Customer; không gọi ngân hàng, không tạo QR/payment request, không nhận callback và không ghi dữ liệu. Nguồn QR và xác minh runtime đều là adapter **Unavailable**; không có cấu hình Development hoặc fake provider nào bật được chúng. Đây là phần đọc thực tế cùng hợp đồng mở rộng và kiểm thử mock, chưa hoàn thành tích hợp thu tiền UC-WEB-07.

## Ma trận yêu cầu

| Yêu cầu / BR | Code và kiểm chứng offline | Trạng thái / việc còn lại |
| --- | --- | --- |
| Đơn WEB thuộc Customer; không dò đơn khác (08) | `CustomerOrderReader.Owned`, `CustomerOrderReadEndpoints.DetailAsync`; policy Customer; 404 chung; test query LINQ/SQL generated, handler và Razor A/B | Hoàn thiện luồng đọc; chưa chạy SQL runtime |
| Số tiền, mã đơn, tham chiếu, hạn, trạng thái | `CustomerOrderReadContracts`, `CustomerOrderReader`, `Payment.cshtml` | Đọc snapshot thật khi API khả dụng; thiếu tham chiếu/hạn được báo rõ, không tự tạo |
| Giá snapshot, số dư (12/36) | Tổng giá món + topping × phần × lượng còn lại + phí hiện tại; thu từ `SoTienDaXacMinh`; hoàn từ lần chi `SUCCEEDED` | Test số học mock; phải đối chiếu SQL và số dư ngân hàng sau |
| Hiển thị QR hợp lệ / không dùng mẫu | `IPaymentQrSource`, `PaymentModel.CanDisplay`; chỉ ảnh PNG nội tuyến khớp payment/amount/reference/deadline, còn hạn, chưa thu | Nhánh hiển thị kiểm thử bằng fixture ảnh, **không phải QR ngân hàng**. Runtime chưa có nguồn QR đã kiểm chứng nên báo chưa hỗ trợ |
| Kiểm tra nội dung mã QR, thụ hưởng và cấu hình nhà cung cấp (18) | Trách nhiệm của adapter tin cậy trước khi trả `ValidatedPaymentQr`; không coi PNG header là bằng chứng QR đúng | Chưa có adapter ngân hàng/cấu hình tin cậy; chưa giải mã/kiểm chứng VietQR thật. Không bật QR runtime |
| Reload / mở lại chỉ truy trạng thái | `GetCustomerOrderAsync`, `PaymentModel.OnGetAsync`; GET-only, no-store, cookie chỉ dùng server | Test snapshot PENDING → VERIFIED khi reload bằng HTTP mock; không tự đánh dấu PAID |
| PARTIAL/VERIFIED/EXPIRED/RECONCILING/FAILED | `CustomerOrderPresentation.PaymentMessage`, hiển thị trạng thái đơn riêng | Theo vocabulary SQL hiện có; `VERIFIED` đủ tiền có thời điểm mới được diễn giải đã xác minh |
| Qua hạn khi chưa có worker (17) | Cảnh báo hiển thị; Phase 2 thêm `OrderLifecyclePlanner.Expire`, clock giả và worker một lượt mặc định OFF | Chỉ lập kế hoạch/mock; `EXPIRY_MAPPING_REQUIRED`, không tự gán CANCELLED, chưa có adapter SQL |
| Nguồn xác minh được xác thực (18) | `IPaymentVerificationSource`; runtime `UnavailablePaymentVerificationSource` luôn trả null | Fake chỉ trong assembly test; chữ ký/bí mật/giao thức nhà cung cấp thật chưa tích hợp |
| Callback hợp lệ/sai/lặp (14/18) | `PaymentVerificationPolicy` đánh giá evidence chuẩn hóa từ adapter; dùng entity `GiaoDichNganHang` có sẵn | Test mô phỏng Exact/Rejected/Duplicate/Conflict. Không có callback endpoint hoặc tài chính writer runtime |
| Thiếu/thừa/sai tham chiếu/tiền muộn | Policy trả Partial/Excess/Reconciliation; không cộng tiền hoặc sửa đơn | Mock đạt; thực nhận, hoàn thừa và audit/outbox nguyên tử chưa được chứng minh |
| Hết hạn và callback cạnh tranh (05/14/17/29) | Phase 2 kiểm thử hai thứ tự bằng fake clock, snapshot version và policy hiện có | Mock interleavings; chưa chứng minh SQL concurrency, worker thực tế hoặc ngân hàng |
| Lỗi mạng, timeout, HTML/rỗng/sai JSON | `CustomerOrderReadApiClient` và PageModel; 502/503/504 có kiểm soát, 401/403 về đăng nhập | Mock tests; không retry/POST khi tra cứu lỗi |
| Bảo mật cookie / dữ liệu máy chung | Không token vào HTML/JS/localStorage; `Cache-Control: no-store`, Vary Cookie | Test HTML không token; không chạm GUID/payload đang chờ của Checkout |

## Ranh giới adapter và tích hợp tiếp theo

`PaymentVerificationPolicy` chỉ là **bộ đánh giá**, không phải cơ chế thu tiền đã hoàn thành. Đầu vào phải là evidence do adapter xác thực tạo ra; không được bind trực tiếp JSON công khai thành `VerifiedBankTransfer`. Kết quả Exact/Excess không cho phép tự gán VERIFIED/PENDING_CONFIRMATION ở trình duyệt.

Trước khi mở runtime cần: chọn nhà cung cấp; kiểm chứng chữ ký/raw body, replay window, tài khoản, chiều IN và VND; kiểm chứng QR mã hóa đúng thụ hưởng/số tiền/tham chiếu/hạn. Writer tương lai phải kiểm tra lại version/trạng thái/hạn, khóa duy nhất giao dịch ngân hàng và liên kết payment; ghi thực nhận, thu thiếu/thừa/muộn, audit/outbox và nghĩa vụ hoàn trong cùng transaction. Lỗi sau giao dịch ngoài phải đối soát, không giả rollback ngân hàng bằng SQL rollback. Worker hết hạn phải xử lý cạnh tranh callback và giữ kho đúng một lần.

Chưa có callback thật, tra cứu ngân hàng, QR production, phát hành PDF hóa đơn hay chi hoàn tiền. Không gọi các API này để xem trang. Chỉ thực hiện integration SQL/ngân hàng sau một phê duyệt khác và môi trường đã được xác minh.

## Kiểm thử

`PaymentOfflineTests` kiểm tra callback giả, thiếu/thừa/lặp/xung đột/muộn/hết hạn, trạng thái và QR presentation boundary. `OrderReadTests` kiểm tra quyền, proxy lỗi và số dư. `OrderRazorOfflineTests` chạy Razor thật với Backend HTTP mock và DataProtection tạm thời, không chạy API/SQL. Tất cả gắn `Safety=NoDatabase`; kết quả và lệnh cụ thể ở `artifacts/uc-web-07-08/RESULT.md`.

## Bổ sung Phase 2

`OrderLifecyclePlanning.cs` không dùng CafeDbContext, không có SQL adapter hoặc đăng ký runtime. `OrderExpiryWorker` là orchestrator gọi rõ ràng từng lượt, không phải hosted/background service; options mặc định OFF và không đọc candidate khi OFF. Không có key cấu hình nào tự khởi động nó. Clock lấy từ `TimeProvider`, dùng hạn đã chốt trên đơn/payment, không tính lại theo giờ reload.

BR-17 trong đặc tả ghi rõ: CSDL chưa có mã EXPIRED cho đơn và **không được tự gán CANCELLED**. Vì vậy đề xuất hết hạn có target SQL null, code `EXPIRY_MAPPING_REQUIRED`, `Applicable=false`, dù điều kiện khác hợp lệ. Các lượng giải phóng/khoản cần hoàn chỉ là đề xuất. Cần chốt ánh xạ riêng trước triển khai adapter SQL/worker; không thay schema hoặc dùng CANCELLED để làm test đạt.

Planner chỉ xét PENDING_PAYMENT, đúng version, đến hạn, còn thiếu tiền; yêu cầu chứng cứ đã đối soát ngân hàng đến hạn từ adapter tương lai. Boolean chứng cứ trong snapshot nội bộ không phải dữ liệu được chấp nhận từ HTTP. Đủ tiền/đã xác nhận/đã tiếp nhận không được timer hủy. Thiếu chứng cứ, giữ kho sai, món đã pha hoặc hồ sơ hoàn cũ đều chặn tự động. Giải phóng chỉ phần giữ còn lại, không trừ tồn thực tế, không ghi PAID. Tests không chứng minh đã giải phóng SQL.

Policy thanh toán thêm fail-closed khi ledger mock có hai bản ghi cùng khóa hoặc liên kết nhầm payment; không tự cộng tiền. Các nhánh đúng hạn/thiếu/thừa/trùng/sai tham chiếu/muộn tiếp tục dùng policy có sẵn, không tạo callback route mới.

`order-snapshot.js` trên order/payment pages cảnh báo offline, ẩn QR cũ và khóa link cần online; nối mạng/BFCache tải lại **toàn bộ document** từ server. Không có event merge/cursor/polling, nên không áp dụng response hay sự kiện cũ vào DOM. Không đọc/xóa pending GUID/payload, không tự gửi lệnh tạo đơn/chuyển tiền. Chi tiết kiểm chứng Phase 2 ở `artifacts/uc-web-07-08/RESULT-PHASE2.md`.
