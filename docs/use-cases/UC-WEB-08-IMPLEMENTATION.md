# UC-WEB-08 — Đơn hàng cá nhân: phạm vi đọc và mock

> Cập nhật tổng review 2026-11-07: SQL fixture V2 đã PASS 26 scenario records cho payment/cancel trực tiếp trên SQL. Các mục Phase cũ giữ lịch sử, không phải kết luận hiện hành. Chưa nghiệm thu HTTP hủy, realtime, nguồn PDF hoặc chi hoàn tiền. Xem `artifacts/uc-web-07-08/SQL-INTEGRATION-RESULT.md` và `artifacts/final-review/UC-WEB-06-07-08-READINESS.md`.



**Cập nhật Phase 4:** Backend CancelAsync đã bổ sung flag OFF, Idempotency-Key, version và audit receipt replay trên transaction hiện có; Razor cancel vẫn khóa cứng. Mô tả “Backend giữ nguyên/chưa có SQL adapter” trong các mục Phase1/2 bên dưới là lịch sử. Chưa kiểm thử ghi SQL hoặc bật cancellation; xem `artifacts/uc-web-07-08/RESULT-PHASE4.md` để biết trạng thái mới nhất.

Đối chiếu `dac-25-Uc.docx`, mục UC-WEB-08, BR-02/04/05/08/13/14/15/17/20/21/22/29/31/33/35/36. Không thay schema/context hoặc logic ghi Backend của thành viên khác.

## Khảo sát và tái sử dụng

Trang cũ dùng JSON thô, lấy tối đa 100 đơn và polling 15 giây, chưa có phân trang/chi tiết. Razor handler Cancel chuyển tiếp POST thật. Backend `CustomerOrderCancellation` đã có giao dịch Serializable, kiểm tra owner/trạng thái, giải phóng giữ kho, hồ sơ hoàn và audit; nhưng request chỉ có `Reason`, chưa có operation GUID/version protocol và chưa chứng minh đầy đủ retry/atomicity/race bằng lượt mock này. Vì vậy handler Web mới **luôn khóa ghi**, không gửi HTTP tới endpoint hủy. Không sửa endpoint hủy Backend.

Tái sử dụng `DonHang`, `ChiTietDonHang`, `MonThemChiTietDonHang`, `ThanhToan`, `HoSoHoanTien`, `LanChiHoanTien`, `HoaDon`. DTO đọc là hợp đồng snapshot mới; không nhân bản DTO Create, receipt hoặc service tính giá Checkout.

## Ma trận yêu cầu

| Yêu cầu | Code / kiểm chứng | Mức hoàn thiện |
| --- | --- | --- |
| Danh sách chỉ đơn WEB thuộc khách | `CustomerOrderReader.Owned`, policy Customer, claim ID dương | LINQ ownership + SQL translation không mở kết nối + handler/Razor mock A/B; SQL runtime chưa chạy |
| Lọc thời gian/trạng thái, phân trang | GET `/api/customer/orders?page=1&pageSize=10&status=...&fromUtc=...&toUtc=...`; `CustomerOrderFilter`; `/Orders` | Hoàn thiện phần đọc; khoảng `[fromUtc,toUtc)`, nhãn UTC, page 1..100000, size 1..50; Web dùng 10 |
| Không phá caller cũ | GET `/api/customer/orders` không query mới vẫn trả array legacy tối đa 100; khi có filter/page trả envelope | Tái sử dụng cùng endpoint; không thêm endpoint lịch sử trùng; hành vi legacy không được dùng cho Web mới |
| Empty và bộ lọc | `Index.cshtml`, link phân trang giữ filter; detail/payment truyền ReturnPage/Status/From/To | Không dùng cache/localStorage; không có dữ liệu mẫu khi rỗng/lỗi |
| Chi tiết / snapshot giá cấu hình (13) | GET `/api/customer/orders/{orderId:long}`, `/Orders/{orderId:long}`; món/topping/đường/đá/ghi chú, số đặt/hủy/pha, phí và tổng | Không đọc lại giá menu, không lộ BOM, token hoặc tài khoản ngân hàng |
| Snapshot đa bảng | Reader dùng AsNoTracking, giao dịch đọc Serializable ngắn cho detail; không SaveChanges | Có cơ chế đọc nhất quán; mức khóa/hiệu năng SQL chưa kiểm thử. Count/list là hai SELECT, có thể lệch khi có ghi đồng thời |
| Tiến trình đơn và tiền tách biệt | Trạng thái đơn, mốc tiếp nhận/hoàn tất; payment status; snapshot UTC | Reload lấy snapshot mới; không dựng mốc lịch sử chưa tồn tại |
| Cập nhật realtime / mất kết nối (35) | Tải lại thủ công; Phase 2 thêm offline warning và full reload khi reconnect/BFCache | **Chưa triển khai realtime hoặc mục tiêu 3 giây**; không merge sự kiện/snapshot cũ |
| Hoàn tiền và số dư (21/36) | `CustomerRefundSnapshot`; Requested/Approved/Succeeded phân biệt; chỉ tổng `LanChiHoanTien.SUCCEEDED` là tiền đã hoàn | Không suy diễn Approved/Processing là đã chi; kiểm thử số học mock, chưa đối chiếu ngân hàng |
| Hóa đơn (20) | Thông tin chứng từ; Phase 2 thêm GET PDF có kiểm tra Customer/owner/ISSUED và Web proxy | Chỉ đọc tệp đã lưu, nguồn runtime Unavailable trả 501; chưa có kho PDF hoặc renderer đã được kiểm chứng |
| UI hủy toàn đơn chỉ PENDING_CONFIRMATION | `CancellationEligible`, Details form reason required 3..400, disabled button; số đã thu chưa hoàn chỉ là thông tin đối soát | UI có; lệnh ghi chưa hoàn thiện. ACCEPTED/PREPARING không có form |
| Chống POST thủ công / CSRF | `RejectCancellationAsync` trả 503 CANCELLATION_NOT_VERIFIED; thiếu cookie 401, lý do sai 400; mặc định CSRF Razor | Test Razor thật: thiếu CSRF 400; CSRF hợp lệ vẫn 503, zero outbound write |
| Customer A không xem/hủy B | Owner trong truy vấn; not-found không phân biệt không có/khác owner; mọi cancel Web đều khóa | Test handler owner, LINQ scope, Razor A/B và cancel A/B. Chưa thử real cancellation SQL, không báo PASS giao dịch |
| Hủy thật / race staff / retry (04/14/22/29) | Phase 2 thêm planner command GUID/version/reason và contract transaction; giữ nguyên Backend ghi hiện có | Mock store kiểm thử replay/lost response/race; chưa có SQL adapter hoặc flag mở khóa Web |
| Lỗi / timeout / trạng thái lạ | API client GET-only có Bearer server, no-store; 401/403 về login; lỗi upstream/HTML/rỗng/JSON/timeout có kiểm soát | Không giữ dữ liệu tài khoản khác, không tự gửi lại lệnh |
| UC-WEB-06 regression | Giữ nguyên Checkout handler/JS; test Razor đọc đơn/payment rồi Checkout vẫn OFF, không outbound Create | Chạy lại toàn bộ 70 test Checkout cũ và test hồi quy mới; không chạy lại integration TCP |

## Hợp đồng và quyền

API xác thực Customer theo middleware sẵn có. Không nhận Customer ID từ query/browser để chọn owner. Detail trả cùng 404 cho ID không tồn tại và ID không thuộc khách; chỉ sau khi kiểm tra owner mới truy vấn con. Projection DTO không chứa tên/điện thoại khách, tài khoản hoàn, khóa thao tác, session, bank evidence hoặc JSON hóa đơn. Phần JSON legacy không thay đổi quyền hiện có và được giữ tương thích.

List trả `{items,page,pageSize,totalCount,snapshotUtc}` khi dùng query mới. Thứ tự `ThoiDiemTao DESC, MaDonHang DESC` ổn định khi trùng thời gian; danh sách có thể thay đổi giữa các lần tải khi phát sinh đơn. Không hứa cursor cố định. Thời gian entity SQL UTC được gắn UTC trước serialize, không chuyển giờ theo máy trình duyệt.

## Còn phải hoàn thiện trước mở ghi

Hủy: operation ID + payload hash/version, idempotent lookup sau timeout, kiểm tra cạnh tranh tiếp nhận, giải phóng chỉ giữ chưa tiêu hao một lần, tính phí/nghĩa vụ hoàn, audit và outbox nguyên tử. Phân biệt tạo RefundRequest với chi thật; không tự PAID/CANCELLED/COMPLETED. SQL/transaction tests phải chạy riêng sau guard môi trường và phê duyệt.

Realtime cần scoped subscription theo Customer, version/cursor, drop duplicate/stale, reconnect snapshot, cảnh báo quá hạn và kiểm thử mục tiêu BR-35. Hóa đơn cần endpoint nội dung/PDF sở hữu đúng quyền và nguồn chứng từ đã phát hành; không tạo URL tải giả. Phần này hiện chưa hoàn thiện.

Không xóa pending GUID/payload/cart của UC-WEB-06 khi xem đơn hoặc thanh toán. Trên máy dùng chung phải đăng xuất, tránh lưu profile browser dùng chung; thao tác chưa rõ cần đối soát đúng tài khoản và mã thao tác trước khi xóa thủ công dữ liệu trình duyệt.

Các lệnh kiểm thử offline và bằng chứng ở `artifacts/uc-web-07-08/RESULT.md`; dữ liệu callback/browser fixture không phải tích hợp ngân hàng/SQL thật.

## Bổ sung Phase 2

`CancelOrderIntent` yêu cầu Customer/order ID, operation GUID không rỗng, expected version và lý do 3–400 ký tự. `OrderLifecyclePlanner.Cancel` kiểm tra owner trước version/trạng thái, chỉ PENDING_CONFIRMATION, chặn món đã xử lý hoặc giữ kho bất thường. Fingerprint giữ nguyên toàn bộ command, kể cả version/lý do; retry chưa rõ phải giữ nguyên GUID và payload. Planner trả đề xuất giải phóng phần giữ còn lại, phí sau hủy 0, số đã xác minh cần REQUESTED refund; không coi đó là kết quả chi hoàn.

`IOrderLifecycleTransaction` mô tả transaction bắt buộc: owner trước replay, operation/hash, đọc lại version của tất cả dữ liệu liên quan, cập nhật đơn/dòng/payment request/kho/refund/audit/outbox cùng commit. **Chưa có SQL implementation hoặc runtime registration**. Backend cancel cũ và Razor khóa ghi không thay đổi. MemoryTransaction chỉ có trong test: replay/lost response cùng receipt, một lần giải phóng, một refund/audit/outbox; cạnh tranh staff theo hai thứ tự và request hủy đồng thời. Đây không phải bằng chứng SQL isolation/crash durability/outbox delivery.

GET `/api/customer/orders/{orderId}/invoices/{invoiceId}/pdf` xác thực Customer và kiểm tra sở hữu qua reader trước khi gọi archive. Chỉ invoice ISSUED có IssuedUtc mới được đọc. Kiểm tra tệp trả về khớp order/invoice/number; PDF envelope/giới hạn 10 MiB, attachment tên số, no-store/nosniff. Web GET `/Orders/{orderId}?handler=Invoice&invoiceId=...` giữ Bearer phía server, giới hạn stream và timeout, không đổi trạng thái. Không có archive trả 501; không đủ quyền/chứng từ chưa phát hành trả 404; lỗi nguồn trả 502/503/504 phù hợp. Runtime chỉ đăng ký `UnavailableInvoiceDocumentSource`.

PDF fixture kiểm tra pipeline/authorization/envelope, **không phải hóa đơn PDF hợp pháp hoặc kiểm chứng nội dung/tệp thực**. Header/EOF không xác minh chữ ký số hay loại bỏ active content; archive adapter tương lai phải là nguồn tin cậy, có tính bất biến và đối chiếu nội dung hóa đơn. Không tái dựng hóa đơn từ giá menu hoặc tự render snapshot JSON khi chưa có hợp đồng nội dung. Chưa có archive/rendering/PDF infrastructure thật, nên chưa thể tuyên bố tải hóa đơn production hoàn tất.

Mất kết nối/BFCache xử lý bằng script full-document reload, không polling/realtime. Không có localStorage mới hoặc merge event; giữ nguyên bộ lọc theo URL, dữ liệu cũ được cảnh báo, QR cũ bị ẩn và link tải PDF bị chặn khi offline. Kết quả build/regression Phase 2 ở `artifacts/uc-web-07-08/RESULT-PHASE2.md`.
