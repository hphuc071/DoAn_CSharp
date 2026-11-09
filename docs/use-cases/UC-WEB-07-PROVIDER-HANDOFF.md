# UC-WEB-07 — Hợp đồng bàn giao tích hợp VietQR/ngân hàng

Trạng thái: **chưa chọn/chấp thuận provider, chưa cho phép triển khai thanh toán thật**.
Tài phụ trách UC01–04; Thái phụ trách UC05–08. Tài liệu này không cấp quyền callback công khai,
ghi SQL, migration, bật flag hoặc xác nhận các quyết định BR-17.

## 1. Căn cứ và phần đang có

Đã đối chiếu `dac-25-Uc.docx`, UC-WEB-07 và BR-08/14/15/16/17/18/21/29/36.
Đơn WEB phải BANK_TRANSFER trả trước đủ đã xác minh mới PENDING_CONFIRMATION. QR/ảnh chuyển khoản/
nút khách không phải chứng cứ thu. Thiếu tiền không được tiếp nhận; thừa tiền là nghĩa vụ cần xử lý,
không tăng tổng giá đơn hoặc doanh thu. Tiền muộn/sai tham chiếu cần đối soát, không phục hồi đơn.

Các thành phần tái sử dụng, không tạo bảng/DTO/service trùng:

- `ThanhToan`: payment intent đã được UC06 tạo nguyên tử cùng đơn/holds; có ID/code, amount,
  reference, deadline, rowversion, operation GUID và `NoiDungQR` tùy chọn.
- `GiaoDichNganHang`: unique `(TenNhaCungCap, MaTaiKhoanNganHangQuan, MaGiaoDichBenNgoai)`;
  lưu giao dịch thực nhận, fingerprint và linkage; không coi giao dịch chưa phân bổ là đơn đã trả.
- `IPaymentQrSource`/`ValidatedPaymentQr`: nguồn QR tin cậy cho trang đọc; runtime Unavailable.
- `IBankCallbackProtocol` → `BankCallbackAdapter` → `IPaymentVerificationSource`: điểm xác thực
  raw payload; runtime không có protocol thật và không có callback route công khai.
- `PaymentVerificationPolicy`, `PaymentConfirmationPlanning`, `SqlBankPaymentService`:
  đánh giá và ghi tiền; SQL writer vẫn bị khóa bằng hai cờ độc lập bên dưới.
- Audit journal, `OrderAuditOutbox`, delivery/lease/retry hiện có; không thêm dispatcher runtime.

**Khoảng trống quan trọng:** UC06 Create hiện để `MaThamChieuChuyenKhoan` và `NoiDungQR` null.
`ThanhToan` chưa có cột snapshot provider/receiver/revision; cấu hình `Bank:Provider` và
`Bank:ReceiverAccount` là profile hiện tại, chưa chứng minh binding bất biến của từng intent.
Không được coi việc có payment intent là đã có yêu cầu VietQR hoàn chỉnh. Không tự dùng order ID,
payment code hoặc free-text evidence field làm protocol/thông tin thụ hưởng khi chưa duyệt.

## 2. Đầu vào cần chủ module/người phụ trách cung cấp

| Đầu vào phải chốt | Ai cung cấp/chấp thuận | Điều kiện chấp nhận |
| --- | --- | --- |
| Provider tạo QR và nguồn xác minh giao dịch (có thể khác nhau) | Phụ trách tích hợp thanh toán + người phụ trách đồ án | Tài liệu API chính thức, sandbox, định danh phiên bản và danh sách capability thực có; không suy ra từ khả năng tạo VietQR |
| Thụ hưởng | Chủ tài khoản/người được ủy quyền | Bank/BIN, account ID/số nhận, tên thụ hưởng, currency VND, revision/hiệu lực cấu hình; xác minh thuộc quán và quy trình rotation |
| Reference | Chủ thanh toán + Backend/Thái | Quy tắc ký tự/độ dài/tính duy nhất, chuẩn hóa theo provider, cách khớp exact, cách lưu/reuse intent; tương thích unique reference hiện có |
| Callback/lookup | Provider + phụ trách bảo mật | Raw-body/signature hoặc chứng thư/token theo giao thức thật; timestamp/timezone, retry/ack, ID ổn định, lookup và retention |
| Thời gian tiền ngân hàng và delayed callback | Người quyết định nghiệp vụ | Ý nghĩa settled/booked timestamp, clock skew, cut-off và chính sách callback đến sau hạn nhưng bank time trước hạn; không tự đặt grace period |
| Thiếu/thừa/muộn/sai reference và hoàn | Phụ trách nghiệp vụ + POS/Admin | Actor đối soát, nguồn chứng cứ, nghĩa vụ hoàn, hành động được phép và giới hạn; không tự SUCCEEDED |
| Hợp đồng môi trường kiểm thử | Người cấp quyền SQL/provider | Container/instance/catalog mới, schema fingerprint, quyền provisioning/migration/DML riêng, sandbox credentials theo kênh bảo mật |

Không đưa credential, tài khoản khách thật, raw callback có dữ liệu cá nhân hoặc QR thật vào Git,
HTML debug, localStorage, query string, log hay báo cáo. Chỉ công bố thông tin thụ hưởng được phép
cho người thanh toán; không gửi API key, session token hoặc bank transaction evidence tới browser.

## 3. Hợp đồng nguồn QR và vòng đời intent

1. Backend xác thực Customer/ownership, đơn WEB/PENDING_PAYMENT, snapshot/remaining/payment
   nhất quán, reference và deadline còn hiệu lực. Không tin số tiền/reference/account do client gửi.
2. Phải có binding durable, review được giữa payment ID, reference, số tiền, order/version,
   receiver/provider revision, thời hạn và định danh yêu cầu bên ngoài nếu có. Chọn cách lưu dựa
   trên thiết kế module được duyệt; mọi thay đổi schema cần migration riêng và quyền riêng.
3. Chuẩn bị/nhận lại intent cũ bằng operation/key cố định; unique reference hiện có bảo vệ DB.
   Sinh/tải QR không tạo nghĩa vụ thu hoặc payment thứ hai. Không tự lấy GUID mới sau timeout.
4. Gọi provider ngoài transaction SQL dài. Phải phân biệt generation thành công, timeout chưa
   rõ và lookup/replay cùng request; không được giả rollback giao dịch ngoài bằng SQL rollback.
5. Nguồn phải kiểm tra/giải mã QR thực tế: đúng bank/receiver, VND, amount, reference và expiry.
   PNG header chỉ là kiểm tra định dạng ảnh, **không** chứng minh VietQR hay thụ hưởng đúng.
6. `IPaymentQrSource.GetAsync` dùng cho trang đọc phải đọc tài liệu đã xác minh của intent hiện có,
   không tạo payment/thu tiền. Trả null khi chưa đủ chứng cứ; deadline UTC explicit, image PNG
   nội tuyến trong giới hạn hiện có. Adapter phải thực thi I/O bất đồng bộ và cancellation.
7. Web chỉ nhận ảnh sau khi đối chiếu payment ID, amount/remaining, reference và hạn order/payment.
   Chỉ PENDING chưa thu mới hiển thị QR hiện tại; PARTIAL cần đối soát/chính sách cấp lại rõ ràng.
   Read timeout5s là giới hạn kỹ thuật, không phải timeout thanh toán hoặc quyền tạo yêu cầu mới.
8. Browser ẩn QR đến hạn, offline hoặc BFCache; không đổi order/payment, giải phóng kho hay gửi
   POST. Reload chỉ tải snapshot. Đồng hồ browser chỉ hỗ trợ hiển thị; quyết định tài chính ở server.

Chưa có cơ chế generate/cache/binding receiver đáng tin trong runtime: **BLOCKED_PROVIDER**.
Không thay `UnavailablePaymentQrSource` trước khi thỏa các điều kiện trên và được duyệt rollout.

## 4. Hợp đồng xác thực callback/lookup

- Chỉ tạo `VerifiedBankTransfer` sau khi provider-specific protocol xác thực raw bytes và header
  theo tài liệu chính thức. Không bind trực tiếp JSON công khai thành evidence tin cậy; không dùng
  cookie Customer hoặc nút “đã chuyển” để chứng minh tiền.
- Chốt với provider cách verify chữ ký/certificate/token, key rotation, chống replay timestamp/nonce,
  skew và lookup độc lập nếu cần. Tài liệu này không chọn thuật toán hoặc phát minh header/HMAC.
- Mapping phải gồm provider/account đã xác minh, stable external transaction ID, IN, VND,
  integer amount>0 trong decimal(18,0), reference raw/canonical được duyệt và bank timestamp UTC.
  Adapter không được tin SQL IDs, linkage, trạng thái xác minh do callback tự khai.
- Giữ canonical identity ổn định qua retry, đối chiếu SQL collation/case/space/precision.
  Không tự lowercase/trim ID hoặc làm tròn timestamp khi chưa biết hợp đồng provider: thay đổi
  canonicalization có thể phá fingerprint/replay. Test datetime2(3) và payload hash thực tế.
- Giới hạn body hiện64KiB; validate metadata trước mở SQL. Không ghi raw body/secrets vào log.
  Malformed/authentication failure phải bị từ chối, không chấp nhận vì Development.
- `SqlBankPaymentService` phát Warning/EventId7001 `BankEvidenceRejected` khi evidence bị từ
  chối, chỉ ghi code cố định `BANK_AUTHENTICATION_OR_RECEIVER_INVALID`. Không log body/header,
  tài khoản/transaction ID hay exception chứa payload; regression kiểm tra chưa mở SQL.
  Đây là application security signal; sink/retention/monitoring production còn cần cấu hình được
  duyệt, không được báo đã có durable security audit/provider incident workflow chỉ vì mock logger PASS.
- ACK/retry và HTTP error contract phải theo provider được duyệt. ACK thành công chỉ sau durable
  commit hoặc replay đã xác minh; timeout/commit chưa rõ tra lại cùng bank identity, không key mới.
  Không gửi callback, webhook, notification hoặc tiền thật trong fixture.

## 5. Đối soát và không tính trùng

| Trường hợp | Code hiện có | Phần cần hoàn tất trước runtime |
| --- | --- | --- |
| Thiếu tiền đúng hạn | PARTIAL; không chuyển order sang hàng tiếp nhận; bank truth ghi một lần | Quy trình Customer/Admin xử lý phần thiếu và cấp QR tiếp theo nếu được duyệt |
| Đủ tiền hợp lệ | VERIFIED/PENDING_CONFIRMATION theo locked snapshot | Provider auth + SQL/HTTP integration cho phiên bản này |
| Thừa tiền | Thu thực nhận, `EXCESS_REQUIRES_RECONCILIATION`; giá/obligation đơn không tăng; RefundObligation thể hiện chênh lệch | Actor/flow đối soát/hoàn và báo cáo không tính excess là doanh thu; không tự payout |
| Trùng cùng identity/nội dung | Trả receipt cũ200, không cộng lần hai | Provider retry canonicalization/lookup và SQL verify trên fixture mới |
| Identity trùng nội dung khác | Conflict409; giữ bằng chứng | Quy trình incident/manual review, không tự overwrite |
| Sai reference, tiền đến muộn, đơn terminal hoặc snapshot không hợp lệ | Ghi bank truth authenticated nhưng không tự allocate/confirm/resurrect; cần đối soát | Admin lookup/linkage/refund với actor thật, audit/idempotency; không coi null linkage là thất thoát hoặc tự bỏ giao dịch |
| Bank time hợp lệ nhưng callback xử lý sau deadline | Code hiện tại bảo thủ: không tự confirm | Chính sách production còn chờ quyết định; không tự nới deadline |
| Auth/time bằng chứng chưa rõ | Không tự xác minh/confirm/expire | Cần nguồn lookup và quyết định nghiệp vụ; BR17 vẫn PROPOSED |

`GiaoDichNganHang.TrangThaiXacMinh=VERIFIED` xác nhận bank truth khi source đã được xác thực,
không đồng nghĩa mọi đơn đã PAID. Thu được phân bổ ở `ThanhToan.SoTienDaXacMinh`; thu chưa phân
bổ không được tự chuyển vào đơn hoặc hủy dấu vết. Hoàn thành công chỉ từ payout đã xác minh,
không từ hồ sơ REQUESTED/APPROVED hoặc callback thu tiền.

## 6. Ranh giới transaction, khóa, audit/outbox

Luồng `SqlBankPaymentService` hiện có:

1. Hai cờ OFF kiểm tra trước source/SQL. Xác thực source và đóng băng bản sao evidence trước await.
2. Resolve candidate reference bằng read ngoài transaction để tránh giữ S-lock khi chờ order lock.
3. Begin SERIALIZABLE; transaction-owned applock theo bank-derived operation; lookup unique
   bank identity + audit/fingerprint và trả replay/conflict trước mutation.
4. Applock `4chang.orderstate.<orderId>` dùng chung cancel/expiry; đọc lại order/payment, lines,
   topping snapshots, refund và ledger. Reference từ pre-read chỉ là candidate, không phải quyền ghi.
5. Kiểm tra lại clock sau khi chờ khóa/đọc; planner phải so amount/deadline/version/state.
   Thiếu tiền không confirm; paid/closed/expired không được phục hồi.
6. Bank row, verified allocation/state và audit journal/outbox stamp được SaveChanges/commit
   cùng transaction. Outbox consumer phải idempotent; dispatcher không auto-host/enabled.
7. Audit lỗi phải rollback cả allocation/bank/event, không có dispatchable event giả. Không provider
   I/O trong transaction, không auto retry sau ambiguous commit và không thay idempotency key.

Unique constraints và applock không tự chứng minh mọi interleaving không deadlock: SERIALIZABLE
có thể giữ range locks khi các bank IDs khác nhau cùng chưa tồn tại. Đây là **rủi ro cần SQL proof**,
không phải kết luận đã quan sát lỗi1205 ở payment. Không tự thêm global lock/nới timeout hoặc sửa
writer đang đúng khi chưa có evidence. Cần test callbacks khác ID cùng/khác order, unmatched,
Create/cancel/expiry và order-reader contention.

## 7. Kế hoạch HTTP/SQL sau khi được cấp quyền riêng

Không sử dụng/reset fixture14331/14332/14333/14335/14339 hoặc môi trường chính14330.
Tạo fixture mới độc lập chỉ khi được cho phép; chứng minh container ID/project/port loopback,
instance/catalog/session marker, schema fingerprint, trusted CHECK/FK/index và login scope trước
mọi lệnh ghi. Không có default/.env connection fallback. Không tự chạy EXPIRED migration.

- Baseline SELECT trước/sau: orders, payment request, bank rows/allocations, balances, audits,
  outbox/delivery, refunds/payouts, holds/BOM và physical stock. Nhận IDs sau SaveChanges.
- Provider sandbox contract vectors: signature/body tamper, wrong/missing account/currency/IN,
  transaction/reference, old/future/ambiguous timezone, duplicate/redelivery và rotation.
- Customer HTTP: anonymous401, sai owner404, CSRF cho command browser nếu có; GET/reload không
  tạo intent. Callback auth riêng theo provider, không bỏ authorization để làm test PASS.
- QR generation/lookup: cùng intent/ref/receiver revision/amount/deadline; không payment thứ hai;
  request bị mất phản hồi giữ operation cũ; dữ liệu sai/timeout/unavailable không hiển thị QR.
- SQL exact/partial cumulative/excess/wrong-reference/unallocated late/duplicate/conflict;
  cancellation/terminal không resurrect; verify event nghĩa vụ chênh lệch, không payout giả.
- Duplicate concurrent cùng bank ID (cùng/khác payload); callbacks khác ID cùng order và khác
  order; unmatched-vs-matched; lock wait/timeout1205 phải lưu evidence và dừng, không auto retry.
- Lỗi audit/outbox, commit-response loss và restart: rollback hoặc replay durable đúng một lần;
  không ghi thu/giữ kho lần hai, không âm thầm ACK khi chưa commit.
- EXPIRED/payment-vs-expiry/zero-paid worker chỉ sau phê duyệt BR17 + migration fixture + DML
  riêng; partial/late expiry phải có quyết định chính sách, không giả bằng CANCELLED.

## 8. Điều kiện bật capability và nghiệm thu

| Capability | Điều kiện tối thiểu | Hiện tại |
| --- | --- | --- |
| Nguồn QR runtime | Provider được duyệt; receiver/reference durable; decode/beneficiary/amount/ref/time proof; bounded lookup; ownership/HTTP/SQL tests; rollout riêng | Unavailable, BLOCKED_PROVIDER |
| `Orders:SqlIntegrationValidated` | Fixture mới, schema/security/rollback/concurrency/audit/outbox và HTTP contract cho code tích hợp được review; quyền rollout riêng | OFF; test lịch sử không tự cấp quyền bật |
| `Orders:PaymentProcessingEnabled` | Toàn bộ điều kiện SQL trên + provider/auth/rotation/replay/time/reconciliation policy và operation monitoring; cờ SqlIntegrationValidated cùng bật chỉ khi được duyệt | OFF |
| Callback HTTP | Provider/protocol/security/ack contract được duyệt, rate/body/auth/log policy và test sandbox/fixture | Chưa đăng ký route; không tự tạo trong lượt này |
| Expiry/read EXPIRED | Phê duyệt BR17 D1–D6, migration/consumers/SQL proof và reconciliation source; quyền riêng cho mỗi bước | PROPOSED; worker OFF |
| Dispatcher/hoàn tiền | Consumer delivery idempotency/ordering + quyền bật riêng; payout provider/actor và evidence thật cho SUCCEEDED | Không bật; không đi kèm payment flag |

Nghiệm thu UC07 cần đồng thời: Customer-owned valid intent/QR thật, auth bank/receiver/time/ref,
thu/thiếu/thừa/muộn/trùng/rollback/concurrency đã kiểm chứng, status recovery không giả PAID,
expiry mapping được duyệt, đối soát/hoàn/POS-Admin đồng bộ và bằng chứng không lộ dữ liệu.
Offline PASS và SQL fixture fake verified events là bằng chứng từng lớp, **không phải ngân hàng
production PASS**. Không có phần nào trong tài liệu này tự phê duyệt provider, migration hay writer.
