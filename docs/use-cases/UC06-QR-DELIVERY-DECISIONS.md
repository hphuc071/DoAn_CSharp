# UC06 — Hồ sơ quyết định QR bàn, giao hàng và BR17

Trạng thái: **PROPOSED / CHƯA PHÊ DUYỆT**. Nhánh `feat/thai-web-cart-order`, checkpoint `45fa1ed`. Không tài liệu nào ở đây cấp quyền ghi SQL, áp dụng migration hoặc bật writer.

Ngày hoàn tất review: 2026-11-08 (Asia/Bangkok).

## 1. Căn cứ và những cơ chế đã có

Đối chiếu trực tiếp `dac-25-Uc.docx`: BR01/02/03/05/08/10/11/12/13/14/16/17/24/29/32/34, UC06 main/A1–A5/E1–E11. BR10 yêu cầu token khó đoán, thu hồi được, kiểm tra khi quét **và gửi**; QR không xác thực Customer. BR01 ghi biểu phí là đề xuất; BR32 ghi TTL 5 phút là đề xuất. Chưa tìm thấy văn bản phê duyệt các con số này trong nguồn/tài liệu triển khai đã review.

| Nguồn thực tế | Có thể tái sử dụng | Không được suy luận |
| --- | --- | --- |
| Entity `Ban`, `BanConfiguration`, schema35 | MaBan, MaBanHienThi, unique MaBamQR binary(32), PhienBanQR, QRHetHanLuc, DangSuDung, rowversion | Không phải bảng phiên ghé bàn; không có Customer owner của bàn |
| DemoSeed | Chỉ là dữ liệu demo bàn | Random digest không có secret tương ứng đã phát hành, không dùng làm issuer |
| Admin `DataEndpoints`, Desktop manage-orders | Admin đọc Ban qua TableReader, hash bị che | Không có route phát hành/rotation/revoke QR hoặc phiên bàn tin cậy; không coi generic Admin grid là issuer |
| `DiaChiKhachHang`, `CustomerSelfService` | Address/coordinates/ownership/version và snapshot trên đơn | Địa chỉ lưu không chứng minh tuyến đường, phí hoặc phạm vi giao |
| `DonHang` | MaBan/MaBanDaChot, recipient/phone/address, lat/lon, distance, policy JSON, HanBaoPhi | Chưa có snapshot QR version/context trong cột riêng; trường có sẵn không chứng minh Create đã ghi đủ |
| Calculator menu / Create / ThanhToan / hold / audit-outbox | Luồng TAKEAWAY và transaction hiện có, operation namespace duy nhất | Không copy thành ba order services, không đổi legacy hash/receipt để thêm proof |
| `FulfillmentPreviewService` | Lookup QR AsNoTracking, protected owner/cart/location/policy/price/deadline proof; read-only adapters | Proof hiện tại không phải permission Create hoặc phiên QR đã nghiệm thu |
| Routing / fee policy | Interface route và cấu hình phí explicit; runtime source unavailable | Không có provider/origin/routing profile hoặc bảng phí quản trị được duyệt đang hoạt động |

Không mở SQL để xác minh dữ liệu live trong lượt này. Schema/code là nguồn thiết kế; bằng chứng SQL cũ giữ nguyên theo phạm vi fixture của nó.

## 2. Quyết định QR cần duyệt

| Mã | Hai lựa chọn và khuyến nghị | Quyền quyết định / điều kiện |
| --- | --- | --- |
| Q1 — ai cấp/thu hồi | A: Admin hiện có; B: Staff có ủy quyền theo ca. **Khuyến nghị A ban đầu**, B cần policy ủy quyền/audit rõ. Customer không được cấp hoặc sửa bàn | Chủ nghiệp vụ duyệt; chưa có issuer endpoint nào được triển khai |
| Q2 — mô hình phiên | A: capability của Ban + context ngắn gắn Customer/giỏ; B: bảng visit/session riêng. **Khuyến nghị A**, tái sử dụng Ban. Nếu cần đóng từng lượt ngồi hoặc thu hồi từng người thì B phải có yêu cầu nghiệp vụ/schema riêng | BR10 cho nhiều khách cùng bàn có đơn riêng; không tự thêm group order hoặc quyền xem đơn cùng bàn |
| Q3 — hợp đồng token | A: random 32-byte secret, base64url canonical, SHA256 lưu SQL; B: token ký nhưng vẫn cần state thu hồi. **Khuyến nghị A**, phù hợp MaBamQR hiện có | Protocol đang là proposal; phải kiểm chứng SQL digest equality và issuer thực tế, không nhận table ID/version từ client làm bằng chứng |
| Q4 — thời hạn/thu hồi | A: hạn QR riêng + short context TTL cấu hình; B: QR tĩnh vô hạn, chỉ rotation. **Khuyến nghị A**, chốt thời hạn và cách xử lý NULL. Không tự đặt số phút | Hiện schema cho NULL, read preview giới hạn bằng TTL cấu hình; điều này không tự phê duyệt QR vĩnh viễn |
| Q5 — vận hành | Rotation/disable ảnh hưởng thao tác mới; đơn đã tạo giữ snapshot. Replay receipt của command đã commit vẫn phải được trả đúng owner, không tạo lại | Duyệt quy trình in/cấp lại/mất QR và quyền dừng bàn; không hủy đơn cũ khi đổi token |

### Thiết kế kỹ thuật khuyến nghị, chưa bật ghi

1. Issuer Admin được xác thực; CSRF nếu dùng cookie, reason + operation GUID + expected rowversion. Trong transaction, kiểm tra quyền/bàn/version, tạo secret bằng CSPRNG, lưu **chỉ digest**, tăng PhienBanQR, ghi hạn/active và audit bắt buộc nguyên tử. Không trả digest như secret. Hiển thị bản secret/QR qua kênh no-store được kiểm soát; mất secret thì rotate, không khôi phục từ hash. Nếu cần phân phối lại qua operation replay, phải chốt kho secret mã hóa/TTL riêng; **không lưu secret vào audit receipt**. Outbox hiện dành order, không giả event Ban thành order event; thông báo issuer cần thiết kế riêng nếu nghiệp vụ yêu cầu.
2. QR đề nghị chứa URL HTTPS cùng origin, secret ở fragment; trang scan không analytics/third-party assets. JavaScript chuyển secret bằng same-origin POST có CSRF rồi xóa fragment bằng replaceState; server/proxy không log body. Fragment không phải phương án bảo mật đầy đủ nếu trang có XSS hoặc extension độc hại. Không đưa token vào query, returnUrl, localStorage hoặc shared browser profile.
3. Handoff trước login: context bảo vệ phía server gắn nonce trình duyệt ngắn hạn bằng cookie HttpOnly/Secure/SameSite, không phải xác thực Customer; hết login xác minh Customer session thật, re-read QR rồi cấp context owner-bound. Nếu cần lưu secret để recheck thì chỉ trong protected cookie/server cache có TTL và không log; phải review key ring/transport. Current preview gửi raw token mỗi lần, **chưa có handoff này**.
4. Sau login, context gắn Customer + table ID/version/digest fingerprint + deadline + giỏ/giá đã báo. Customer A logout/B login không kế thừa pending của A. Nhiều khách dùng cùng QR được phép theo BR10, nhưng mỗi command/receipt/order có owner riêng. Token có thể bị sao chép: không khẳng định đó là bằng chứng hiện diện vật lý.
5. Đổi bàn của giỏ chưa gửi: xác minh token mới trước, trình khách xác nhận chuyển; bỏ confirmation/proof cũ, báo lại tổng, không sửa command đang chưa rõ kết quả. Refresh/multitab không sinh GUID mới tự động. Scan lại token hợp lệ là đọc hợp lệ, **không phải one-time token**; chống trùng order nằm ở command GUID/payload và transaction.
6. Create tương lai: hợp đồng additive/versioned riêng cho fulfillment context; không đổi fingerprint DTO TAKEAWAY lịch sử. Giữ exact GUID/payload/proof/accepted total từ lần xác nhận. Authorize owner → operation lock/replay → nếu chưa có order, kiểm tra context, table active/version/expiry dưới lock giữ đến commit; rotation/revoke phải cạnh tranh cùng row/version. Re-read giá/BOM/tồn, snapshot table ID/code + QR version/fingerprint trong audit snapshot không có secret, order/payment/hold/audit/outbox cùng transaction. Chỉ thêm cột snapshot nếu có yêu cầu query đã duyệt; không tạo bảng phiên trùng lúc này.
7. Replay sau token/context hết hạn hoặc bị thu hồi: nếu command đã commit thì trả receipt cũ cùng owner/hash trước expiry validation; nếu chưa có thì từ chối và giữ nguyên command chưa rõ kết quả để đối soát. Nếu writer OFF thì từ chối, không tự bypass flag để replay. Không dùng receipt cũ làm bằng chứng đã PAID hoặc QR hiện còn hợp lệ.

## 3. Quyết định DELIVERY cần duyệt

| Mã | Lựa chọn / khuyến nghị | Điều kiện |
| --- | --- | --- |
| L1 — nguồn khoảng cách | Provider road-routing + store origin được cấu hình; hoặc chưa hỗ trợ giao. **Khuyến nghị adapter server tới provider được chọn riêng**, không tự chọn nhà cung cấp | Duyệt provider/origin/profile/quota/timeout và xử lý điểm mơ hồ; không straight-line/client km/giá mẫu |
| L2 — fee/radius/free | Phê duyệt hoặc sửa đề xuất BR01: ≤3km 15k, đến10km +ceil(km−3)*5k, miễn ≥300k trong5km | Chủ nghiệp vụ duyệt đầy đủ, gồm basis tiền hàng hợp lệ, inclusive boundaries và chính xác distance. Các số trên chỉ là phương án, không runtime default |
| L3 — version/quản trị | A: immutable policy qua deployment có review; B: SQL fee-policy + Admin CRUD. **Khuyến nghị A cho gói tối thiểu**, tránh bảng/service trùng khi chưa cần quản trị runtime | Approval flag không thay thế hồ sơ phê duyệt. Snapshot cấu hình atomic/immutable, không đọc mixed values khi reload |
| L4 — TTL | Duyệt hoặc sửa 5 phút đề xuất BR32; hạn thực tế min(policy TTL, routing validity) | Không lấy order PendingPayment deadline thay quote expiry. Không tự gia hạn quote cũ khi recheck |
| L5 — độ chính xác/phạm vi | Đề nghị route theo mét, chuẩn hóa thành km decimal tương thích KhoangCachGiaoKm decimal(10,3); chốt xử lý provider precision trước fee boundary | Không round xuống qua ngưỡng ceil rồi giữ phí cũ. Cần provider contract/fixture; chưa áp dụng quy tắc làm tròn mới |

### Luồng kỹ thuật khuyến nghị

1. Nhận Customer từ session, không từ body. Nhận contact/address + điểm khách xác nhận; nếu nhận saved-address ID thì lookup ownership/version phía server. Free-form address hiện có trong preview không đồng nghĩa có một address ID được sở hữu hoặc đã geocode.
2. Adapter lấy route từ store origin đã cấu hình tới điểm giao đã xác minh, từ chối địa chỉ mơ hồ/không định tuyến được. Không có source thì 503, ngoài phạm vi thì từ chối; không mặc định 0. Không gọi routing/ngân hàng trong SQL transaction dài.
3. Chụp immutable policy version + toàn bộ tham số, origin/profile/provider revision và kết quả route/điểm đích. Tính tiền hàng bằng calculator hiện có, phí bằng policy, total = goods + fee bằng decimal. Current read proof đã bind owner/full request/policy/km/price/expiry; provider revision/origin binding là điều kiện phải bổ sung khi có adapter thật, vì interface hiện chưa cung cấp các revision này.
4. Trả context/proof báo giá no-store, gắn Customer/contact/address/coordinates/cart/version/policy hash/distance/amount/expiry. Customer chấp thuận **tổng chính xác**. Đổi bất kỳ dữ liệu nào hoặc expired phải từ chối/requote và xin chấp thuận lại; fresh route có cùng fee nhưng policy/source thay đổi không tự nhận là quote cũ.
5. Create versioned tương lai giữ original proof trong immutable payload; trước bất kỳ ghi mới nào phải recheck owner, exact accepted total và quote validity/route-policy revision; lock/recheck đủ ngắn rồi snapshot recipient/phone/address/lat-lon/km/policy JSON/HanBaoPhi và BOM. Ghi order/payment/hold/audit/outbox trong một transaction, pending payment chỉ BANK_TRANSFER, không COD. Địa chỉ hoặc menu đổi sau đó không sửa snapshot.
6. Replay command đã commit trả receipt cũ, không gọi routing rồi tính phí mới, không tạo payment/hold lần hai. Không rõ kết quả thì chỉ replay/tra operation cũ, không tự mint proof/payload/GUID thay thế. Receipt/status live tải qua owned read API; expired quote không có nghĩa order đã EXPIRED.

Hiện Create DELIVERY hard-locked. Không thể mở chỉ bằng khai `Approved=true` hoặc bật Web flag; cần triển khai/review transaction và bằng chứng mới.

## 4. BR17 và phối hợp các module

Giữ hồ sơ gốc `BR-17-EXPIRY-DECISION.md`, proposals up/down review-only và D1–D6 **chưa duyệt**. Khuyến nghị EXPIRED terminal riêng; không gán CANCELLED. Không tự giải quyết partial-paid actor bằng giả Customer/Admin. Không coi callback muộn tự phục hồi order.

| Module | Công việc trước rollout EXPIRED |
| --- | --- |
| SQL/EF | CK_DonHang_7 từ 11→12 states, trusted WITH CHECK; entity string không cần enum mới; dữ liệu cũ không backfill. Không status FK; kiểm tra FK/trigger/data live trên fixture riêng |
| Payment/refund | Chốt boundary + bank watermark, effective obligation/Remaining; partial-paid expiry và REQUESTED refund có actor hợp lệ, không SUCCEEDED khi chưa chi |
| Inventory | Release remaining unconsumed một lần, không hồi TonThucTe/BOM; audit/outbox failure rollback; replay/restart không release lại |
| POS/Admin | Sửa open-order predicates ở WorkEndpoints/PosDeviceService sau quyết định; báo cáo không gộp EXPIRED vào completed/cancelled. Dashboard thu VERIFIED/PARTIAL/excess cần đối chiếu BR30/36 riêng |
| Web/API | Read allowlist/filter/labels theo read flag; không lấy deadline để tự hiển thị status terminal khi SQL chưa chuyển; PDF không phát hành giả |
| Audit/outbox | EXPIRE_ORDER envelope đã có code; SQL fixture outbox proof không thay expiry SQL proof. Chọn sink/inbox thật, giữ retry/ordering/dedup; không reset lifecycle event sequence |

Checklist sau phê duyệt: fixture migration mới có container/port/effective connection/DB identity/schema guard; backup/restore; wrong-target/missing approval/drift fail; up/down trust/check exact set; zero-paid expire/replay/hold-once; audit/outbox rollback; worker/payment race hai chiều; late money no resurrection; partial-paid chỉ sau actor policy. Down refuse nếu có EXPIRED rows, ưu tiên forward-fix, không đổi data thành CANCELLED. Giữ worker không hosted và flags OFF đến đủ evidence. Không chạy bất kỳ bước SQL nào trong lượt review này.

## 5. Quyền triển khai hiện tại và điều kiện mở khóa

| Hình thức | Hiện có / được checkpoint | Trước mở runtime |
| --- | --- | --- |
| TAKEAWAY ASAP | Code SQL/HTTP đã chứng minh trước, recovery; unchanged hash/payment/hold transaction | Explicit Web + API flags trên môi trường đã xác minh; giờ phục vụ/điểm nhận và hẹn giờ còn thiếu nếu nghiệm thu đủ A3 |
| QR_TABLE (`DINE_IN`) | Read-only verify/proof/mock, UI khóa | Q1–Q5, issuer/handoff, live SQL hash/version/rotation, Create atomic integration + HTTP/SQL replay/concurrency/ownership; không flag nào mở hiện tại |
| DELIVERY | Read-only validation/pricing/proof/mock; unavailable source | L1–L5, origin/provider/policy, UI/map và snapshot transaction, SQL/HTTP stale/replay/hold/rollback; không flag nào mở hiện tại |

Có thể sửa ngay lỗi kỹ thuật trong code read-only, hoàn tất review/checkpoint và chuẩn bị adapter/transaction contract. Cấp QR thật, chọn provider, áp dụng fee/TTL, đổi schema/EXPIRED, partial expiry actor và mở writer bắt buộc chờ quyết định/quyền tương ứng. Không tạo thêm fake provider runtime hoặc vòng mock để thay integration.

Cập nhật hardening accepted-total ngày 2026-11-08: đã xác minh route Create này chỉ dành Customer WEB, không phải POS/Admin; Web client hiện luôn gửi header. Backend nay bắt buộc một `X-Checkout-Accepted-Total` decimal không âm hợp lệ trước SQL nghiệp vụ, cả khi replay. Thiếu/sai header trả400; command mới có tổng khác server tính trả409 CHECKOUT_PRICE_CHANGED trước bất kỳ ghi nào. Receipt replay cũ phải đúng owner/hash và đúng tổng gốc, khác tổng trả409 CHECKOUT_ACCEPTED_TOTAL_CONFLICT; không đổi hash DTO hoặc schema/audit lịch sử. Receipt gốc chính là bằng chứng bind tổng đã commit, không dùng preview proof thay accepted total. Các lần đọc giá/menu/BOM/snapshot được đặt trong transaction SERIALIZABLE hiện có sau operation lock, tránh đọc giá và snapshot ở hai phiên bản khác nhau. Header không phải nguồn tính tiền; server vẫn tự tính từ menu/topping/quantity và ship TAKEAWAY=0. Không fallback cho caller Customer thiếu header; caller đó cần bổ sung tổng đã được khách chấp thuận. Đây là sửa guard theo đặc tả, không tạo hợp đồng POS/Admin mới. Regression offline kiểm chứng protocol/replay; không có SQL write mới và chưa tái kiểm chứng transaction boundary/concurrency trên fixture, vì vậy chưa mở writer. Bằng chứng checkpoint ở `artifacts/uc-web-06/UC06-CHECKPOINT-RESULT.md`.

Người phê duyệt, ngày, phạm vi Q/L/BR17: **CHƯA CÓ**. Phê duyệt tài liệu không tự bật feature flag, không tự cấp quyền migration hoặc ghi fixture.
