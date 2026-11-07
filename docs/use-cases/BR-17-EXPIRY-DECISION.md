# BR-17 — Biểu diễn hết hạn chờ thanh toán

> Hồ sơ trình duyệt sau SQL fixture V2, 2026-11-07: **PROPOSED / NOT APPROVED**.
> Hồ sơ đề nghị phê duyệt đầu tài liệu là cập nhật hiện hành; các mục Phase 3/4/5 bên dưới giữ bối cảnh lịch sử.
> V2 chứng minh payment/cancellation trực tiếp trên SQL, không chứng minh expiry hoặc migration.
> Ma trận nghiệm thu: `artifacts/final-review/UC-WEB-06-07-08-READINESS.md`.

> Hardening tiếp theo: có envelope bền vững trong audit và SQL delivery-store/dispatcher code chưa kích hoạt; xem `ORDER-OUTBOX-DESIGN.md`. Delivery migration chưa duyệt/chạy, chưa có SQL outbox proof/production sink. Phần lịch sử “chưa có outbox” mô tả trước hardening; BR17/quyền chạy migration vẫn chưa duyệt.

## Hồ sơ đề nghị phê duyệt hiện hành — sau fixture V2

**PROPOSED / NOT APPROVED.** Các mục Phase 3/4/5 bên dưới giữ lịch sử. Kết luận cũ “chưa chạy SQL” không còn áp dụng cho payment/cancel: V2 có 26 scenario records PASS trực tiếp trên SQL. Kết luận đó vẫn đúng cho migration/expiry. Không có SQL hoặc migration mới được chạy trong lượt tổng review.

### Danh mục tác động chính xác

| Thành phần | Nguồn cần đồng bộ/kiểm chứng | Tác động |
| --- | --- | --- |
| SQL canonical | `docs/database/35-bang/schema.sql`, `model.json` cùng thư mục | `CK_DonHang_7`: 11 → 12 states, thêm EXPIRED bằng WITH CHECK, trusted/enabled. Không status FK, không cần đổi FK hiện có; không backfill |
| EF/entity | `src/Backend/FourChang.Infrastructure/Database/Configurations/Orders/DonHangConfiguration.cs`; `src/Backend/FourChang.Domain/Entities/Orders/DonHang.cs` | TrangThai là string, không có enum trung tâm; đồng bộ CHECK/varchar(25), giữ rowversion/deadline. Không tự dùng HoanTatLuc làm mốc expiry |
| Payment/holds | `Configurations/Payments/ThanhToanConfiguration.cs`, `Configurations/Inventory/GiuChoNguyenLieuConfiguration.cs` dưới Infrastructure/Database | Đã có EXPIRED riêng; không thêm lại. Payment EXPIRED không chứng minh order đã terminal |
| Partial/refund actor | `CK_HuyMon_3`, `CK_HoSoHoanTien_4` trong schema35 | Bắt buộc một Customer/Staff/Admin, không SYSTEM. Giữ `PARTIAL_EXPIRY_ACTOR_POLICY_REQUIRED`; không giả mạo Customer hoặc âm thầm nới CHECK |
| API expiry | `src/Backend/FourChang.Api/Features/Customer/Orders/SqlOrderExpiryWorker.cs`, `OrderLifecyclePlanning.cs`, `OrderIntegrationSafety.cs` | Schema proof, bank watermark/version, order lock, operation/audit; zero-paid adapter, không hosted; planner giữ fail-closed |
| Read/API balance | `CustomerOrderReader.cs`, `CustomerOrderReadEndpoints.cs` cùng thư mục; `Features/Customer/Payments/PaymentVerification.cs`, `PaymentConfirmationPlanning.cs`, `SqlBankPaymentService.cs` | Late money không phục hồi terminal. Chốt effective obligation khi EXPIRED: snapshot giá còn nguyên có thể khiến Remaining > 0; nhãn UI không thay hợp đồng tài chính API |
| Shared/Web | Shared `Contracts/Customer/CustomerOrderReadContracts.cs`, `ApiClients/Customer/CustomerOrderReadApiClient.cs`; Web `Services/CustomerOrderPresentation.cs`, `Pages/Checkout/Payment.cshtml.cs`, `Pages/Orders/Details.cshtml` | Allowlist/filter/nhãn và opt-in `Orders:ExpiryStatusReadEnabled`; read flag không mở writer |
| POS/ca | API `Features/Staff/WorkSessions/WorkEndpoints.cs`, `Features/Admin/Access/PosDeviceService.cs` | Open-order predicates hiện chỉ loại COMPLETED/CANCELLED/REJECTED; phải kiểm tra EXPIRED nếu gắn ca/bàn giao. WEB create hiện không gắn ca, không khẳng định mọi ca đang bị chặn |
| Admin/báo cáo | API `Features/Admin/Data/DataEndpoints.cs`; Desktop Admin/Reports views | Tách expiry/cancel/completed. Dashboard hiện cộng VERIFIED, trừ payout SUCCEEDED; chưa tách thu thực tế/doanh thu/nghĩa vụ, bỏ PARTIAL và có thể gộp excess. Cần kiểm chứng BR-18/36, không sửa ngoài phạm vi hồ sơ |
| Bootstrap/legacy | Infrastructure `Database/Initialization/DatabaseBootstrap.cs`; manifest35; nguồn schema34 lịch sử | Count không chứng minh CHECK expression. Không sửa schema34 để giả migration, không tự thay đổi trigger |
| Tests | `tests/FourChang.Tests/KhachHang/UC-WEB-07-08/Phase4IntegrationPreparationTests.cs`, `Phase3SafetyTests.cs`, `OrderLifecycleOfflineTests.cs`, `PaymentOfflineTests.cs`, `OrderReadTests.cs`, `OrderRazorOfflineTests.cs`; `tests/FourChang.SqlVerification` | Migration up/down/drift, terminal/read compatibility, release/replay, callback race hai chiều, partial policy, audit/outbox rollback. V2 chưa có expiry DML |

Đường dẫn rút gọn trong bảng tính theo module đã chỉ rõ, không yêu cầu tạo service/DTO mới. EXPIRED dài 7 ký tự phù hợp varchar(25), dữ liệu cũ 11 states vẫn hợp lệ. Không tự đổi historical CANCELLED theo deadline. FK DonHang/children liên kết ID, không status; vẫn phải xác minh 73 FK/11 trigger và definition/trust sau migration, không chỉ đếm.

### Các quyết định phải được phê duyệt riêng

| Mã | Đề nghị/câu hỏi | Trạng thái |
| --- | --- | --- |
| D1 | Chọn A: order EXPIRED terminal riêng thay B: CANCELLED + marker bền vững. A rõ nghĩa hơn, ít phụ thuộc join audit/report; không triển khai B ngầm | CHƯA DUYỆT |
| D2 | Boundary: hiện xử lý ở/sau hạn đưa đối soát kể cả bank timestamp trước hạn. Chốt cùng watermark/lookup của provider | CHƯA DUYỆT |
| D3 | Partial-paid expiry: actor hợp lệ, nghĩa vụ và REQUESTED refund; không SUCCEEDED trước chi | CHƯA DUYỆT |
| D4 | Effective amount/Remaining và timestamp kết thúc EXPIRED; giữ giá/BOM/audit lịch sử | CHƯA DUYỆT |
| D5 | Durable outbox hoặc event source/checkpoint/idempotent dispatcher tái sử dụng audit cho UC06/07/08 | CẦN CHỐT THIẾT KẾ |
| D6 | Provisioning môi trường migration riêng và quyền chạy up/down; không dùng DB lịch sử hoặc fixture V1/V2 | CHƯA CẤP QUYỀN |

Người phê duyệt/ngày/phạm vi: **chưa có**. D1 không tự cấp quyền D2–D6 hoặc bật runtime.

### Migration/rollback trình review, không thực thi

Hai proposal up/down hiện pin **FourChangBr17MigrationFixture**, một đích tương lai chưa được tạo ở lượt này. Bỏ pin cũ vào DB lịch sử FourChangPhase3CTest để tránh dùng dữ liệu đã lưu bằng chứng. Tên DB và SESSION_CONTEXT approval **không thay thế** container/port/effective connection guard. Không có port guard bên trong hai script; runner/deployment phải chứng minh riêng trước khi cho chạy.

1. Duyệt D1–D5, giữ writers OFF. Sau phê duyệt provisioning, chứng minh instance/container ID/project/loopback port/database/schema fingerprint; loại trừ 14330/FourChangDb, 14331 historical, 14332 V1, 14333 V2. Baseline SELECT và backup/restore proof.
2. Test wrong DB/missing approval/transaction lồng/schema drift/untrusted đều fail trước ALTER. Up nhận đúng 11 states baseline, transaction thay duy nhất CK_DonHang_7 bằng WITH CHECK, không backfill. Đồng bộ canonical EF/schema/manifest và compatibility readers/POS/Admin trước writer.
3. Test zero-paid expiry/restart/replay đúng một lần, giữ BOM/TonThucTe, release held; lỗi audit/outbox rollback; callback thắng và expiry thắng; tiền muộn ghi bank truth không resurrect. Partial chỉ chạy sau D3. Không thay bằng mock.
4. Down chỉ sau dừng writers và SELECT không còn EXPIRED; nhận baseline 12 states, khôi phục CHECK 11 states trusted. Nếu còn EXPIRED thì từ chối down; giữ schema/reader tương thích, forward-fix. Không map CANCELLED, phục hồi hold hoặc xóa audit để rollback.
5. Chỉ đề nghị mở worker sau SQL/HTTP và bank reconciliation proof; có batch/monitoring/stop switch. Migration PASS không tự bật `SqlIntegrationValidated`, `ExpiryEnabled`.

**Giới hạn bằng chứng:** V2 hủy release 10g một lần, tồn thực tế 1000g không đổi, audit failure rollback và replay unchanged. Đây là đường CANCELLED dùng chung helper; chưa chứng minh đường EXPIRED, worker/payment race hoặc migration DDL. Late V2 là PENDING_PAYMENT quá hạn, không phải row EXPIRED. Policy/mock chặn terminal resurrection; SQL EXPIRED vẫn BLOCKED_BR17.

**Trạng thái: PROPOSED — CHƯA PHÊ DUYỆT.** Ngày 2026-11-07. Tài liệu chỉ đề xuất; không sửa schema/entity/context hoặc kích hoạt worker. `EXPIRY_MAPPING_REQUIRED` tiếp tục chặn áp dụng kế hoạch hết hạn. Phê duyệt thiết kế không đồng nghĩa cấp quyền migration/ghi dữ liệu.

## 1. Cơ sở và phạm vi bằng chứng

BR-15: Web chỉ PENDING_CONFIRMATION sau khi thu đủ đã xác minh; hủy toàn đơn khác với hết hạn chờ tiền. BR-17: chỉ WEB/PENDING_PAYMENT có hạn cấu hình, kết thúc chờ và giải phóng giữ chưa tiêu hao một lần; không timer-hủy đơn đủ tiền/đã tiếp nhận; tiền muộn vẫn phải ghi nhận và đối soát, không phục hồi đơn. BR-18: xác thực nhà cung cấp/account/currency/IN/reference/transaction ID; QR không phải bằng chứng thu tiền. UC06 tạo snapshot và holds; UC07 xử lý tiền/expiry; UC08 theo dõi trạng thái và nghĩa vụ hoàn. Đặc tả `dac-25-Uc.docx` ghi rõ chưa được tự thêm EXPIRED hoặc gán CANCELLED khi chưa chốt.

Đã đối chiếu **code/schema/manifest trong repository**, không kết nối SQL. Không chứng minh CHECK/FK/data hiện đang triển khai ở bất kỳ server nào. Bằng chứng HTTP201/200 đơn 7 và BLOCKED_ENVIRONMENT là lịch sử các lượt trước, không dùng làm kết quả Phase 3. Không suy luận một đơn đã hết hạn/xóa holds chỉ vì deadline đã qua.

## 2. Cấu trúc thực tế

| Nguồn | Kết quả khảo sát |
| --- | --- |
| `FourChang.Infrastructure.csproj` + `DatabaseBootstrap.cs` | Embed/use `docs/database/35-bang/schema.sql` và `model.json`; 34 bảng chỉ là nguồn upgrade cũ. Không dùng bản thiết kế gợi ý/SQL tiếng Anh để kết luận schema hiệu lực |
| `35-bang/schema.sql`, `DonHangConfiguration.cs` | CK_DonHang_7 cho DRAFT, PENDING_PAYMENT, PENDING_CONFIRMATION, ACCEPTED, PREPARING, READY, DELIVERING, COMPLETED, REJECTED, CANCELLED, DELIVERY_FAILED. Không có EXPIRED |
| `DonHang.cs` | TrangThai là string varchar(25) theo mapping, không có enum đơn dùng chung; HetHanLuc, HoanTatLuc, rowversion; chưa có trường phân loại hết hạn riêng |
| `ThanhToanConfiguration.cs` | Payment: PENDING, PARTIAL, VERIFIED, EXPIRED, FAILED, RECONCILING. Không có PAID SQL; payment EXPIRED không chứng minh order đã kết thúc |
| `GiuChoNguyenLieuConfiguration.cs` | Holds: RESERVED, PARTIAL, CONSUMED, RELEASED, EXPIRED. EXPIRED tại hold không phải order status |
| FKs của DonHang | Customer, shift, table, self-related order; children ChiTietDonHang/ThanhToan/HoSoHoanTien/HoaDon tham chiếu MaDonHang, không FK tới một status table; thêm một giá trị status không đổi các FK này |
| Triggers/schema | Audit/BOM/biến động kho bất biến, hóa đơn ISSUED bất biến, WEB chỉ BANK_TRANSFER, liên kết refund đúng order/payment. Không thấy trigger tự hết hạn đơn; không sửa/bỏ trigger để triển khai expiry |
| `OrderExpiryReview.cs` | Admin read-only candidates theo deadline/PENDING_PAYMENT; canAutoExpire=false, yêu cầu đối soát ngân hàng |
| `OrderLifecyclePlanning.cs` | Kế hoạch semantic có release/refund đề xuất, không target SQL, Applicable=false; worker one-shot OFF, không đăng ký hosted service hoặc transaction adapter |
| `CustomerOrdersEndpoints.cs` / `OrderIdempotency` | Deadline từ cấu hình 1..1440 phút, receipt/idempotency giữ kết quả tạo gốc; không tính lại hạn khi replay. Không thay UC06 |
| `CustomerOrderReadContracts` / `CustomerOrderPresentation` | Allowlist hiện không có order EXPIRED, filter từ allowlist; chưa nâng cấp thì reader sẽ từ chối unknown state thay vì giả thành công |
| `WorkEndpoints.cs`, `PosDeviceService.cs` | Kiểm tra đơn còn mở bằng loại trừ COMPLETED/CANCELLED/REJECTED. Cần rà soát terminal state khi thêm EXPIRED; không suy rộng mọi terminal cho POS mà chưa kiểm tra nghiệp vụ ca |
| Admin `DataEndpoints.cs` | Bảng đọc generic; dashboard đếm COMPLETED, thu VERIFIED, chi SUCCEEDED. Expiry không phải doanh thu COMPLETED; cần KPI expiry riêng và kiểm tra thu PARTIAL/RECONCILING độc lập, không sửa dashboard tài chính trong lượt này |
| Desktop POS/Admin/Reports views | Có nhiều UI/demo strings cũ CONFIRMED/FULFILLMENT_FAILED; không coi demo là endpoint nghiệp vụ hoàn thiện. Cần cập nhật nhãn/filter/tập terminal khi triển khai chính thức |
| Tài liệu cũ | `THIET-KE-DATABASE-GOI-Y.md` có EXPIRED khái niệm; không cấp quyền thay CK hiện tại. `34-bang`, SQL tiếng Anh và bản gợi ý không phải migration tự động |

`DatabaseBootstrap.ValidateAsync` kiểm tra bảng/cột/FK và số CHECK/trigger, **chưa so từng biểu thức CHECK**. Sau migration phải kiểm tra thêm `sys.check_constraints.definition`, trạng thái trusted/enabled, không coi chỉ count đúng là chứng minh mapping mới đúng. Không sửa validator/context trong lượt này.

### Dữ liệu lịch sử cần xác minh sau khi được phép

Chưa chạy SELECT live; chưa biết có CANCELLED do hết hạn hoặc holds EXPIRED/order PENDING_PAYMENT trong dữ liệu thực tế hay không. Cần snapshot có thời điểm/đích SQL đã xác minh: phân nhóm order status/channel; pending qua hạn; payment amounts/status/expiry; holds reserved/released/consumed; audit EXPIRE_ORDER/CANCEL_ORDER và operation; refunds/payouts. Không xuất token/PII/bank secrets.

Không backfill chỉ dựa `HetHanLuc < now`, `CANCELLED` hoặc payment/hold EXPIRED. Chỉ lịch sử có bằng chứng transaction/audit rõ mới có thể đề xuất chuyển loại; những dòng mơ hồ giữ nguyên và lập danh sách đối soát. Không cập nhật/xóa lịch sử bất biến.

## 3. Các phương án

| Tiêu chí | A — Bổ sung order EXPIRED | B — Order CANCELLED + dấu vết expiry có cấu trúc |
| --- | --- | --- |
| Ý nghĩa | Terminal riêng: hết hạn chờ tiền; không đồng nghĩa khách/staff hủy | Cần chốt rõ CANCELLED bao gồm expiry; UI/report luôn phân biệt subtype |
| Schema | Mở rộng CK_DonHang_7, giữ varchar(25), không đổi FK; đồng bộ EF/schema/manifest | Có thể giữ CK, nhưng phải có reason machine-readable, audit append-only EXPIRE_ORDER và operation ổn định. Nếu thêm cột/index reason vẫn cần migration riêng |
| Đọc/report | Lọc trực tiếp status; nhãn hết hạn rõ ràng | Phải join/resolve audit mỗi lần để tách cancellation vs expiry; không dùng GhiChu tự do làm nguồn đáng tin |
| Cạnh tranh | Rowversion/order lock + bank key/operation, chỉ pending -> expired | Cùng yêu cầu concurrency, không đơn giản hơn ở money/stock |
| Giữ kho/thanh toán/hoàn | Không phụ thuộc tên status: release còn lại một lần, đóng request, giữ actual credits; partial/late/excess chuyển reconciliation/refund | Tương tự A; không được gọi logic customer-cancel cũ rồi coi là expiry |
| POS/Admin | Phải nâng cấp terminal predicates, labels, list filters | Ít đổi allowlist nhưng dễ tính sai số khách hủy, cần subtype xuyên suốt |
| Lịch sử | Không tự đổi CANCELLED cũ. Rows không rõ vẫn giữ nguyên | Không suy subtype từ deadline. Thiếu marker đáng tin thì không phân loại được |
| Rollback | Không thu hẹp CHECK khi còn EXPIRED; data-aware, giữ compatibility reader | Có thể giữ reader subtype/audit, không xóa audit; rollback code cũ sẽ mất phân biệt nghiệp vụ |
| Chi phí/rủi ro | Thay đổi schema nhỏ nhưng phạm vi consumers rộng, dễ kiểm chứng status | Ít schema ban đầu nhưng coupling audit/read/report và nguy cơ đồng nhất expiry với customer cancellation |

B không được dùng đơn thuần `CANCELLED + GhiChu = hết hạn`: ghi chú có thể sửa và không bảo đảm uniqueness/replay. Cần contract marker bền, bất biến, truy vấn được, ghi cùng transaction; structured audit có sẵn có thể tái sử dụng **sau khi phê duyệt**, nhưng không mặc định có index/unique operation/outbox đầy đủ.

Một phương án giữ PENDING_PAYMENT mãi và chỉ gắn nhãn quá hạn không đáp ứng hậu điều kiện kết thúc/giải phóng của BR-17; chỉ dùng hiện nay như trạng thái fail-closed khi chưa có mapping/trust.

## 4. Đề xuất trình phê duyệt

**Đề xuất A — EXPIRED chính thức**, vì có thể phân biệt ngay với hủy chủ động, hỗ trợ filter/report/audit rõ hơn, không cần suy ngược subtype từ audit khi đọc đơn. EXPIRED chỉ là trạng thái đơn, không phải bằng chứng đã nhận/hoàn tiền. Giữ payment/hold status độc lập. Không có thay đổi mapping nào được áp dụng trong Phase 3 này.

Các điểm cần chủ dự án phê duyệt riêng:

1. Chọn A hay B; nếu B, chốt subtype/marker/nguồn đọc và phân loại báo cáo.
2. Giao thức transaction/operation/replay, trường thời điểm kết thúc và dấu vết expiry bắt buộc; không tự gán ý nghĩa mới cho HoanTatLuc khi chưa thống nhất.
3. Chính sách chính xác khi tiền ngân hàng đến trước deadline nhưng callback/tra cứu xử lý sau deadline hoặc cạnh tranh worker. Hiện policy bảo thủ trả đối soát khi xử lý ở/sau hạn; không tự thay thành chấp nhận chỉ dựa timestamp ngân hàng.
4. Nghĩa vụ partial/late/excess và hồ sơ hoàn, dữ liệu lịch sử được phép sửa/backfill; không thay thực thu hoặc phát hành lại chứng từ.
5. Migration/rollback test và rollout POS/Admin/Web; việc phê duyệt tài liệu không bật worker.

## 5. Invariants và tác động liên UC

| Khu vực | Invariants khi triển khai sau phê duyệt |
| --- | --- |
| UC06 | Create/replay GUID/hash/receipt gốc giữ nguyên, không gia hạn đơn hoặc dựng GUID mới khi timeout; live snapshot khác receipt lịch sử |
| PaymentRequest (`ThanhToan` hiện có) | Không tạo request thứ hai vì reload/QR timeout. Giữ requested amount/ref/account/hạn đã chốt, actual verified immutable theo ledger; không dùng UI PAID |
| Bank/callback | Xác thực provider; dedupe composite provider/account/transaction ID; thiếu chưa confirm, excess tách nghĩa vụ, wrong ref/late không phục hồi terminal order; lưu thu thật khác với xác nhận đơn |
| Worker | Candidate chỉ WEB/PENDING_PAYMENT tới hạn; trusted reconciliation watermark đủ; đọc lại order/payment/holds/refund/version trong transaction; conflict thì không retry bằng mã mới |
| Holds/BOM | Chỉ release reserved − released − consumed hợp lệ theo nghiệp vụ. Whole-order planner hiện chặn consumed/processed để đối soát; không đổi TonThucTe hoặc recipe/BOM lịch sử. Release đúng một lần, không hồi hold vì late callback |
| Refund | REQUESTED không là SUCCEEDED/REFUNDED; chưa chi thì không trừ vào tổng đã hoàn. Ledger thu và payout truth độc lập status order |
| UC08/Web | Nhãn/filter history expiry, không hiện form customer-cancel ngoài PENDING_CONFIRMATION; owner server trên mọi read/PDF/command; reconnect full snapshot, không dùng stale event để hạ version |
| POS/ca | Chỉ nhận đơn đủ verified money vào hàng đợi; không nhận/pha EXPIRED. Nâng cấp kiểm tra terminal ở close/recover/bàn giao, không dùng fallback unknown=active/success |
| Admin/report | Tách hết hạn và hủy chủ động, không tính expiry là COMPLETED revenue. Giữ thu thực tế và hoàn theo transaction/time, không xóa thu muộn vì order terminal |
| Invoice/PDF | Không phát hành PDF/invoice do expiry/view/reload. Invoice ISSUED và audit bất biến, archive trusted; nếu chưa có source thì chưa hỗ trợ |

Một transaction tương lai phải xử lý: lock operation/key theo scope, owner/scope trước replay, đọc version + nghĩa vụ hiện tại, xác thực watermark/ledger, tính kế hoạch, cập nhật status/request/holds/refund, audit + durable operation receipt + outbox rồi commit. HTTP/provider call ngoài transaction dài; khi response mất, lookup cùng operation/payload. Chưa có SQL adapter/outbox hoàn chỉnh; không coi memory lock là bằng chứng SQL.

## 6. Kế hoạch migration A — CHỈ SAU PHÊ DUYỆT

1. Đóng writer/worker/cancel bằng cấu hình và deployment policy; sao lưu/kiểm chứng restore, ghi baseline/check definitions/rowversion/constraints/FKs/trigger; guard đúng SQL test. Không dùng init-db/seed/reset.
2. Chuẩn bị compatibility readers trước: allowlist/filter/nhãn Web/API/shared, POS/Admin/report/terminal predicates, test unknown status và mixed-version rollout. Không reader cũ nào được nhận trạng thái nó sẽ hiểu sai; chưa mở expiry writer.
3. Soạn migration versioned review riêng để mở rộng **chỉ** CK_DonHang_7 cho EXPIRED, giữ trusted CHECK bằng validation đầy đủ; cập nhật schema35, model35 và EF mapping đồng bộ. Kiểm tra upgrade path34/35 có chính sách rõ, không âm thầm sửa schema34 lịch sử. Không thêm status FK/enum trùng khi chưa cần.
4. Xác minh CHECK expression/trust, dữ liệu hiện có, FK/trigger counts/definitions và rollback trên bản sao SQL test; migration thất bại phải dừng. Trước commit DDL phải biết kế hoạch khôi phục transaction/backup và thời gian lock; không công bố lock-free.
5. Không backfill dữ liệu cũ theo tuổi đơn. Mọi đề nghị xử lý historical candidates cần report/bằng chứng và batch phê duyệt riêng; bảo toàn audit/invoice/BOM.
6. Implement writer/outbox/durable operation uniqueness theo kiến trúc đã duyệt; test expiry/callback/cancel/accept, late/partial/excess, inventory and audit atomicity. Schema thành công vẫn giữ OFF nếu trust/payment integration chưa đạt.
7. Chỉ mở worker bằng phê duyệt deployment riêng sau SQL integration PASS, giới hạn batch/observability/fail-stop. Không dùng Development làm chứng cứ test DB, không auto-enable từ migration.

Không tạo/chạy DDL hoặc migration script thực thi trong lượt này. Các bước trên là kế hoạch review, không lệnh vận hành đã được cấp quyền.

### Rollback

Ưu tiên rollback **code writer** về OFF và giữ expanded schema/readers tương thích (forward-compatible), vì mất mapping không cho phép đổi lịch sử. Nếu chưa có row EXPIRED và xác minh live sau ngừng writers, mới cân nhắc migration thu hẹp CHECK sau phê duyệt; kiểm tra lại trusted state và tất cả consumers.

Nếu đã có EXPIRED, **không thu hẹp CHECK, không map hàng loạt sang CANCELLED, không phục hồi PENDING_PAYMENT/holds, không xóa audit/refund/bank rows**. Giữ schema/read compatibility, điều tra và forward-fix. Data transformation hoặc full restore đòi hỏi quyết định riêng; restore có thể mất giao dịch mới và không đảo được chuyển khoản ngoài SQL. Rollback B cũng không xóa marker/audit; code cũ chỉ được dùng nếu không làm mất phân biệt subtype.

## 7. Kế hoạch kiểm thử và điều kiện mở

- Offline hiện tại: EXPIRED chưa có trong allowlist SQL, planner vẫn EXPIRY_MAPPING_REQUIRED, không target CANCELLED; tiền thiếu/untrusted/mislinked/late/duplicate không đề xuất confirm mới; wrong channel/owner/version/reason/GUID bị chặn; UI OFF/CSRF và UC06 regressions.
- Sau phê duyệt A: thêm tests CHECK accept EXPIRED/reject unknown, FK/trigger/schema manifest consistency, terminal allowlist, filter/render/read recovery, legacy/mixed-version consumers, POS open-shift guards, Admin/report phân loại tiền/expiry. Chưa thêm EXPIRED vào code hiện tại chỉ để tests này PASS.
- SQL test sau guard/phê duyệt: B0/B1/B2, hết hạn một lần, replay receipt, batch restart/failure rollback, audit/outbox required, release theo BOM/còn giữ/không tiêu hao, race worker vs verified callback tại boundary/late delivery, khóa/version deadlock handling; expired order không được accept/prepare/resurrect. Không thử trên SQL chính.
- Bank sandbox sau chọn provider/phê duyệt: signed raw callbacks/replay protection, transaction ID uniqueness, recipient/currency/reference, delayed/out-of-order delivery, trusted reconciliation watermark, partial/excess/late obligations; QR generation alone không đạt mục này.
- PDF cần archive chứng từ thực: ownership/cache/attachment, immutable file-to-invoice binding, integrity/content/security review. Fixture envelope không chứng minh invoice hợp pháp.

Điều kiện đóng BR-17: phê duyệt mapping + chính sách boundary/late callbacks, migration/rollback được review/test, đồng bộ readers/consumers, writer/operation/audit/outbox và trust bank được kiểm chứng. Trước đó mọi kế hoạch hết hạn tiếp tục fail-closed.

## 8. Chuẩn bị Phase 4 — không phải phê duyệt

Đã thêm hai script **review-only** tại `docs/database/proposals/BR17-EXPIRED.up.sql` và `.down.sql`. Bản trình duyệt hiện tại chỉ nhận tên database tương lai `FourChangBr17MigrationFixture`, không nhận database lịch sử `FourChangPhase3CTest`. Yêu cầu SESSION_CONTEXT approval riêng cho up/down và dedicated session; không backfill. Down từ chối nếu còn EXPIRED rows. Chưa chạy hai script; schema35/model/EF CHECK gốc chưa đổi. Các artifact SELECT-only Phase4 xác minh live test DB vẫn có 11 status cũ, CHECK trusted; không EXPIRED. Tên database không thay thế bằng chứng cô lập container/port/instance.

Read compatibility có `Orders:ExpiryStatusReadEnabled` riêng trên API/Web, mặc định false, không mở writer. `SqlOrderExpiryWorker` đã có SQL adapter cho **chưa thu tiền**, nhưng không hosted/route; yêu cầu SqlIntegrationValidated + ExpiryEnabled + ExpiryMappingApproved + ExpiryConsumersValidated, schema CHECK đúng và bank proof khớp version/đến deadline. Hiện tất cả OFF, source bank proof Unavailable. Login ứng dụng hiện không thấy CHECK definition; schema readiness sẽ từ chối, không tự GRANT quyền.

Đơn đã thu một phần tiếp tục chặn `PARTIAL_EXPIRY_ACTOR_POLICY_REQUIRED`: CHECK của HuyMon và HoSoHoanTien đòi đúng một actor Customer/Staff/Admin, không SYSTEM. Không giả mạo Customer để auto refund và không lặng lẽ sửa constraint. Cần phê duyệt riêng actor/obligation policy. Zero-paid expiry adapter giữ snapshot món/giá, không tạo Customer cancellation rows hoặc tự ghi HoanTatLuc; nghĩa vụ số dư của EXPIRED cần đối soát, Web opt-in không hiển thị số tính từ giá snapshot như khoản cần chuyển.

Bank/cancel SQL services có transaction + audit receipt/idempotency, nhưng chưa được integration-test; không có transactional outbox dispatcher hoặc bank protocol thật. Mapping/read/SQL permission/actor policy và integration gates nêu trên đều phải hoàn thiện trước kích hoạt. Chi tiết bằng chứng và giới hạn ở `artifacts/uc-web-07-08/RESULT-PHASE4.md`.

## 9. Review Phase 5 — chưa phê duyệt hoặc áp dụng migration

Khảo sát SQL bằng runner .NET SELECT-only xác minh instance test `127.0.0.1:14331`, Docker project `fourchang-phase3c`, SQL MachineName khớp container. Instance chỉ có user database `FourChangPhase3CTest`, đang giữ 6 đơn WEB/PENDING_PAYMENT lịch sử. Chưa có fixture/database độc lập để ghi; **BLOCKED_SQL_TEST**. Quyền CREATE DATABASE của login quản trị chỉ chứng minh khả năng kỹ thuật, không phải phê duyệt provisioning. Không dùng các đơn cũ làm fixture hoặc tạo database/schema mới trong lượt này.

Hai proposal up/down đã bổ sung guard so biểu thức CHECK với đúng tập trạng thái baseline (up 11, down 12), ngoài tên/trust/approval/DB. Guard từ chối schema drift thay vì âm thầm drop một CHECK có nội dung khác. Script vẫn review-only, chưa được parse/execute bằng SQL Server, chưa chứng minh DDL/rollback concurrency. Chưa đổi schema35/context/EF hoặc mapping EXPIRED; migration vẫn cần gói deploy đồng bộ và phê duyệt riêng.

Kế hoạch SQL sau migration: kiểm tra trusted CHECK nhận EXPIRED/từ chối unknown, reader Web/API opt-in, POS/Admin terminal/shift predicates, expiry vs callback cả hai thứ tự khóa, zero-paid release/replay đúng một lần, partial actor/refund policy, late/unmatched ledger không phục hồi đơn, audit failure rollback, nhiều worker/callback contention. Không gán expiry thành CANCELLED/COMPLETED; không backfill theo deadline hoặc thu hẹp CHECK khi còn EXPIRED. Rollback ưu tiên tắt writer và giữ compatibility reader/schema mở rộng. Chỉ thử down khi dừng writers và không có EXPIRED, với phê duyệt rollback riêng.

Runner `tests/FourChang.SqlVerification` gọi service thực tế và có chế độ fixture SQL độc lập cho payment/cancel; không có expiry DML, không hosted worker hoặc provider production. Chi tiết điều kiện provisioning, giới hạn, fault injection test-only và các tình huống chưa chạy ở README runner và `artifacts/uc-web-07-08/RESULT-PHASE5.md`. Không coi mock/compile là SQL integration PASS.
