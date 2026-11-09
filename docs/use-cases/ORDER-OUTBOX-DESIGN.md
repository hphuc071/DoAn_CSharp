# Order audit outbox — triển khai và giới hạn

Trạng thái: **producer + delivery E2E PASS trên fixture14335; dispatcher production OFF**.

> Closure V2 đã đạt `PASS_SQL_OUTBOX` và `PASS_HTTP_CUSTOMER` trong TestServer với SQL fixture mới,88 assertion. Xem `artifacts/final-review/INTEGRATION-CLOSURE-V2.md`. HTTP dùng đăng nhập/API/Razor cookie thật, không fake auth; delivery at-least-once với consumer fixture idempotent. Không suy ra production inbox/provider/host configuration đã nghiệm thu. Đoạn closure14334 bên dưới là lịch sử thất bại, được giữ để đối chiếu.

> Cập nhật integration closure: outbox UP đã áp dụng duy nhất trên fixture mới14334/FourChangOutboxMigrationFixture, CHECK trusted. Lượt ghi dừng tại session setup trước HTTP Customer/dispatcher; không phải PASS end-to-end. Xem `artifacts/final-review/INTEGRATION-CLOSURE.md`. Không thay quyền triển khai production hoặc BR17. SQL delivery UPDATE nay đi qua EF interceptor trong cùng claim transaction; query raw còn read-only.
Thiết kế kỹ thuật này không chốt BR-17, không cấp quyền migration hoặc bật writer.

## Nguồn bền vững và transaction

Không tạo thêm event table cạnh audit. `OrderAuditOutbox.Stamp` gắn envelope `outbox` version1 vào `NhatKyHeThong.DuLieuSauJson`; giữ snapshot cũ, receipt/hash/idempotency fields. EventId là MaThaoTac, unique theo `UX_NhatKy_ThaoTac`. Audit bất biến theo trigger/schema35. Bốn producer dùng đúng audit đã có ở lần SaveChanges cuối trong transaction nghiệp vụ:

| Producer | Event | Giới hạn |
| --- | --- | --- |
| Customer Create | order.created | Không đổi BOM/holds/accepted total/receipt |
| Verified bank writer | bank.transfer.recorded | Linked order hoặc bank:operation khi unallocated; event không đồng nghĩa confirm |
| Customer cancellation | order.cancelled | Snapshot giữ refundRequested, không refund SUCCEEDED |
| SQL expiry adapter | order.expired | Chỉ sau mapping/schema/bank proof gates; chưa chạy |

Không publish/provider call trong transaction. Rollback audit loại envelope cùng nghiệp vụ; replay không Stamp/Save lại. Closure V2 mới chứng minh SQL envelope/rollback/replay và delivery trên14335; 26 ca payment/cancel V2 cũ vẫn là bằng chứng riêng, không trộn số test.

Envelope gửi sink chỉ có version/EventId/aggregate/kind/UTC time, không raw audit/receipt/contact/token. Đây là invalidation để đọc snapshot, không financial command. Không broadcast public group theo orderId. Adapter realtime tương lai phải kiểm owner server và chỉ trả snapshot được phép.

Refund REQUESTED hiện nằm cùng cancellation nên order.cancelled báo snapshot/nghĩa vụ đổi. Không phát minh refund.succeeded/payout/invoice/staff.status.changed cho writer chưa tích hợp. Các luồng ấy cần producer/transaction riêng được review; phạm vi event hiện tại chưa phủ toàn hệ thống.

## Delivery state — migration riêng chưa chạy

`SqlOrderOutboxDeliveryStore` dùng SQL parameterized, không sửa DbContext/entity/schema35. Bảng `OrderOutboxDelivery` chỉ lưu delivery state, FK AuditId, PK chống trùng, attempt/lease/status constraints. Proposal up/down tại `docs/database/proposals/ORDER-OUTBOX-DELIVERY.*.sql` chỉ nhận future DB `FourChangOutboxMigrationFixture`, approval SESSION_CONTEXT riêng. Chưa tạo container/DB này. Runner phải chứng minh container/port/effective connection/schema riêng trước DDL; tên DB không đủ.

Không áp dụng lên 14330/14331/14332/14333 hoặc dữ liệu lịch sử/V1/V2. Bootstrap/manifest35 chưa mở rộng cho bảng mới, cần đồng bộ sau phê duyệt trước deployment. Up không backfill/seed; down từ chối nếu có delivery rows, ưu tiên giữ history và dispatcher OFF.

Dispatcher/store **không đăng ký DI/hosted service/route**. Cần cả `Orders:SqlIntegrationValidated`, `Orders:OutboxDispatchEnabled`, `Orders:OutboxSchemaValidated` true, mặc định khóa. Store kiểm lại gate mỗi Claim/Complete/Fail. Development không mở chức năng.

## Retry, duplicate, ordering và poison

- Một internal delivery pipeline; claim SERIALIZABLE + app lock riêng, lease120s/Guid fencing. Không giữ transaction khi gọi sink.
- At-least-once: sink thành công nhưng ACK mất có thể gửi lại cùng EventId. Consumer production cần durable inbox/dedupe; HashSet trong test chỉ là fake.
- Không dùng high-water cursor trên global audit ID: identity allocation khác commit order. Query tìm journal chưa ACK và chặn event sau cùng aggregate nếu event trước chưa SENT. Producer hiện hữu khóa order-state; create phải commit mới có order cho writer sau đọc. Không hứa total ordering xuyên đơn hoặc cho writer chưa tích hợp.
- Lease expired được claim lại; tối đa5 attempts; backoff attempt²×10s. DEAD hoặc lease thứ5 hết hạn chặn aggregate để đối soát. Không tự skip/delete poison/history. Aggregate khác có thể tiến; cần operator workflow/monitoring trước rollout.
- ACK/Fail yêu cầu đúng token còn lease. Stale worker không ACK lease mới; lỗi mất lease truyền ra, không ép SENT. Cancellation để lease hết hạn, không false ACK.
- Chỉ lưu error code cố định, không exception text có thể chứa secrets. Envelope lỗi/version không hỗ trợ không gửi sink/ACK, đi retry/dead.

## Checklist sau phê duyệt riêng

1. Review audit-event protocol, delivery migration và consumer durable inbox; schema compatibility/indexes/performance/retention. Chứng minh môi trường migration mới, không dùng fixture đã chốt.
2. SQL up/down empty/drift/approval/target/FK/CHECK/trust; down có rows phải từ chối. Đồng bộ manifest/validator sau duyệt.
3. SQL create/payment/cancel success/rollback/replay: đúng một envelope, audit failure rollback cả tiền/hold/event. B0/B1/B2; expiry chỉ sau BR17.
4. Hai dispatchers cạnh tranh; cùng/khác aggregate; commit-order gap; lease expiry/process death/lost ACK/stale token; poison5 lần/recovery không xóa audit. Xác minh SQL state, không dùng fake PASS thay thế.
5. Consumer durable inbox duplicate/order recovery/ownership A-B; không dùng invalidation để tự PAID/payout hoặc sinh chứng từ. Chưa có production sink nên dispatcher OFF.
6. EXPIRED: duyệt D1–D6 trong BR17; đồng bộ CHECK/EF/manifest/readers/POS/Admin; test worker vs payment cả hai chiều, release một lần, partial actor/refund, late no-resurrection, audit/event rollback. Không đổi sang CANCELLED hoặc chạy worker khi chưa đạt.
