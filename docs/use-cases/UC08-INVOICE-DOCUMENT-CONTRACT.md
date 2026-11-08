# UC-WEB-08 — Hợp đồng phát hành và lưu trữ PDF nội bộ

Ngày khảo sát: 2026-11-08. Code base: `68742e1`, nhánh `feat/thai-web-cart-order`.

**Hiện trạng: BLOCKED_INVOICE_PROVIDER.** Đây là đối chiếu hợp đồng hiện có và đề xuất
kết nối, không phải xác nhận đã có service phát hành/kho PDF hoặc phê duyệt triển khai.
Không chạy SQL, migration, phát hành hóa đơn hoặc thay đổi thanh toán trong lượt khảo sát.

## 1. Căn cứ và phạm vi

Đặc tả gốc `dac-25-Uc.docx`:

- BR-20: số hóa đơn duy nhất, snapshot bất biến; chỉ phát hành khi đơn đã nhận, không còn
  dòng chờ nhận và thu đủ; không sửa/xóa bản đã phát hành, dùng điều chỉnh tham chiếu gốc.
  Bản trước trả tiền phải ghi Tạm tính; chưa bao gồm hóa đơn điện tử pháp lý.
- UC-POS-05: Staff có phiên ca hợp lệ, ca OPEN, đơn trong phạm vi được xử lý. Bước 6–8 kiểm
  tra nguồn, thông tin người mua, cấp số/snapshot/audit; lặp trả chứng từ cũ. Đơn WEB đã trả
  trước có nhánh phát hành riêng, không thu lại. A4 cho dựng PDF từ snapshot đã phát hành.
- UC-ADM-08: Admin có quyền hóa đơn/điều chỉnh; điều chỉnh cần nguồn hợp lệ, lý do, số mới
  liên kết bản gốc; in/xuất không phát hành hoặc thu thêm lần nữa.
- UC-WEB-08 A2: chỉ xem chứng từ của đơn mình và tải PDF đã có; chưa phát hành thì thông báo
  chưa có. Thao tác xem không phát hành hóa đơn.

**Kết luận phạm vi:** PDF hóa đơn nội bộ là phù hợp đặc tả; không cần tự mở rộng sang VAT,
cơ quan thuế hoặc chứng thư pháp lý. Điều này không cấp quyền DML/migration và không tự
phê duyệt lựa chọn lưu trữ, mẫu nội dung, API phát hành hoặc quyền vận hành.

## 2. Những gì thực sự tồn tại

| Thành phần | Bằng chứng và giới hạn |
| --- | --- |
| `HoaDon` | FK `MaDonHang`, `SoHoaDon` duy nhất, `LoaiHoaDon` ORIGINAL/ADJUSTMENT, liên kết bản gốc/nguồn, snapshot JSON, số tiền, người mua, `PhatHanhLuc`, quản lý/ca, rowversion. Không có blob, locator hay external document ID. |
| CHECK/index | `CK_HoaDon_1..5`: loại, quan hệ gốc, DRAFT/ISSUED, ISSUED cần thời điểm, JSON hợp lệ. `UX_HoaDon_Goc`: một ORIGINAL trên đơn. CHECK không thay thế kiểm tra đủ tiền, quyền hoặc đủ điều kiện phát hành. |
| Bất biến SQL | `TR_HoaDon_DaPhatHanh` cấm sửa/xóa bản từng ISSUED. Không bảo vệ bytes ở kho ngoài; rowversion không phải phiên bản PDF. |
| API POS/Admin | Có route GET dữ liệu `HoaDon`; chưa có service/endpoint phát hành và kho PDF thật. Desktop lập/xuất hóa đơn vẫn là preview. |
| API Customer | `CustomerOrderReader.Owned` lọc Customer và kênh WEB trước khi tải chứng từ; route PDF kiểm tra Customer, ownership, ISSUED và thời điểm phát hành. |
| Nguồn đọc | `IInvoiceDocumentSource.ReadAsync(orderId, invoiceId, ct)` trả `ArchivedInvoicePdf(OrderId, InvoiceId, InvoiceNumber, Content)` hoặc null. Runtime đăng ký `UnavailableInvoiceDocumentSource`. |

## 3. Hợp đồng phát hành cần triển khai — PROPOSED

Không có bằng chứng code hiện tại thực hiện chuyển DRAFT → ISSUED. Quy trình yêu cầu:

1. Xác thực actor phía server. ORIGINAL theo UC-POS-05: Staff có phiên ca OPEN và phạm vi
   đơn hợp lệ. ADJUSTMENT theo UC-ADM-08: Admin có quyền và nguồn điều chỉnh hợp lệ.
   Không suy ra Customer hoặc mọi Admin đều có quyền phát hành ORIGINAL.
2. Nhận operation ID chống trùng và expected version; kiểm tra command lặp/payload conflict.
   Khóa và đọc lại đơn, dòng hiệu lực, khoản thu đã xác minh, hoàn/điều chỉnh và ca trong
   transaction. Không lấy ảnh chuyển khoản, PaymentRequest PENDING hay tổng client làm thu.
3. Kiểm tra đơn đã được tiếp nhận, không còn dòng chờ nhận, đủ thu theo nghĩa vụ server;
   không diễn giải đơn hết hạn/hủy là đủ điều kiện. Mapping trạng thái/dòng cụ thể phải được
   chủ module đối chiếu với quy trình POS, không chỉ kiểm tra một chuỗi `PAID`.
4. Chốt snapshot hóa đơn độc lập từ dữ liệu nghiệp vụ thật, thông tin người mua đã xác nhận,
   số duy nhất, loại, nguồn, actor/ca và thời gian server. Không dùng giá menu hiện tại để
   tái dựng chứng từ đã phát hành, không sửa snapshot đơn hàng.
5. Ghi hóa đơn ISSUED, audit và journal/outbox trong cùng transaction theo cơ chế hiện có.
   Unique constraint và operation ID là hàng rào cuối khi hai yêu cầu cùng phát hành.
   Chưa chốt thuật toán cấp số hoặc thêm schema trong tài liệu này.
6. Lỗi phát hành không đảo ngược khoản thu đã xác minh và không yêu cầu trả lại lần nữa.
   Retry cùng thao tác phải trả cùng chứng từ; không tự tạo số/đơn/thanh toán khác.

`MaQuanLyPhatHanh` và `MaCaLamViec` hiện có không tự xác lập cách lưu actor Staff.
Chủ module cần chốt mapping Staff/ca/audit theo schema hiện có, hoặc xin duyệt schema nếu
không biểu diễn đủ; không dùng ID quản lý giả hoặc nâng quyền Staff để vượt FK.

## 4. Lưu trữ và mapping — PROPOSED

Khuyến nghị kho PDF nội bộ private, tách khỏi `wwwroot`; lưu bytes bất biến theo định danh
server cấp. Không nhận đường dẫn hoặc URL từ Customer. Có thể là kho đối tượng hoặc kho
file được quản lý, nhưng chưa chọn sản phẩm, đường dẫn, credential hoặc chính sách retention.

Luồng tối thiểu đề xuất: sau transaction phát hành, consumer nội bộ đọc snapshot ISSUED
đã chốt, dựng PDF và lưu bền vững; sau khi bytes và metadata đã hoàn tất mới công bố mapping.
Customer GET chỉ đọc, không render/generate/issue. Trong khoảng chưa có PDF, trạng thái hóa
đơn vẫn phản ánh nghiệp vụ thật, download báo chưa sẵn sàng; không trả PDF trống.

- Dùng journal/outbox hiện có; không đăng ký consumer/worker runtime trong gói này.
- Tạo tệp tạm rồi publish nguyên tử, hoặc immutable object version với conditional create.
  Cấm overwrite sau publish bằng ACL/chính sách kho; hash chỉ phát hiện thay đổi, không tự
  ngăn sửa. Không bật public bucket, không dùng URL công khai lâu dài.
- Consumer deduplicate theo invoice/document identity; retry đọc cùng snapshot và trả cùng
  artifact đã publish. Crash giữa lưu bytes và publish mapping có thể tạo orphan riêng tư;
  không công bố tệp chưa hoàn tất hoặc xóa bằng chứng tự động.
- Khi mapping đã publish, không tái render để ghi đè dù renderer thay phiên bản. Điều chỉnh
  là hóa đơn/tài liệu riêng liên kết bản gốc, không thay bytes của bản gốc.
- Mapping cần FK/unique hoặc cơ chế tương đương, bền vững và có audit. Nếu cần bảng mới,
  phải review migration riêng; không nhét locator tùy ý vào JSON ISSUED đã bị khóa.

### Metadata bắt buộc cho nguồn mới

Đây là hợp đồng đề xuất cho kho, **chưa phải trường đã có trong entity/interface**:

| Trường | Quy tắc |
| --- | --- |
| OrderId, InvoiceId, InvoiceNumber | Khớp `HoaDon.MaDonHang/MaHoaDon/SoHoaDon`; không coi PaymentRequest ID là InvoiceId. |
| DocumentId / immutable locator | Định danh private do server quản lý, một artifact chính cho mỗi hóa đơn; external ID chỉ cần nếu nguồn có. |
| MIME | Chính xác `application/pdf`. |
| ByteLength | Độ dài bytes thực, >16 và ≤10 MiB theo endpoint hiện tại. |
| SHA-256 | Hash bytes cuối cùng; so sánh khi đọc với metadata được bảo vệ, không chỉ tin hash do cùng response tự khai. |
| IssuedUtc | Đối chiếu `HoaDon.PhatHanhLuc`; không thay bằng thời gian dựng file. |
| ArchivedUtc | Thời gian publish archive thành công, có thể sau IssuedUtc. |
| DocumentVersion, RendererVersion, SnapshotDigest | Phân biệt phiên bản artifact/mẫu/snapshot; không dùng rowversion SQL làm version PDF hoặc làm chữ ký pháp lý. |

## 5. Hợp đồng truy xuất Backend

Adapter hiện có thể triển khai interface hiện hữu mà không thêm endpoint Customer:

1. Route GET `/api/customer/orders/{orderId}/invoices/{invoiceId}/pdf` xác thực và kiểm tra
   ownership/ISSUED trước khi gọi nguồn. Interface không tự nhận CustomerId, nên không được
   expose nó trực tiếp hoặc bỏ qua bước owner khi tái sử dụng cho caller mới.
2. Adapter lookup mapping private từ order/invoice, đối chiếu số/thời gian đã phát hành;
   đọc với credential server, budget/cancellation và size cap trước khi cấp phát lớn.
3. Kiểm tra hash/length/MIME và binding; trả bản bytes bất biến qua `ArchivedInvoicePdf`.
   Interface hiện chưa mang SHA/MIME/version nên adapter phải tự kiểm tra; endpoint chỉ
   kiểm envelope PDF, không chứng minh chữ ký, nội dung pháp lý hoặc nguồn gốc.
4. Không tự retry vô hạn, generate-on-read, tự cấp số, chuyển ISSUED, PAID hay chi hoàn.

| Trường hợp | Hợp đồng HTTP hiện tại |
| --- | --- |
| Chưa xác thực / sai role | Middleware 401 / 403. |
| Không sở hữu, không tồn tại, DRAFT, thiếu IssuedUtc | 404; không gọi kho để tránh dò chứng từ. |
| Chưa tích hợp nguồn hoặc `ReadAsync` trả null | 501 hiện tại. Null không đủ phân biệt nguồn chưa hỗ trợ với tài liệu chưa archive; cần chốt semantics trước khi thay status. |
| Binding/metadata/PDF envelope sai | 502. Adapter mới phải biểu diễn lỗi integrity theo contract được review, không để exception riêng rơi thành 500 hoặc lộ locator. |
| HTTP/IO/timeout nguồn, hết budget Backend | 503; budget chung lookup + đọc là 20 giây. |
| Client hủy | Truyền cancellation, không tự retry hoặc ghi tài chính. |
| Proxy Web hết timeout / nguồn HTTP lỗi | Timeout 504; mạng/IO 503; MIME/length/bytes không hợp lệ 502. |

Không đổi 501 hiện tại thành 404/503 âm thầm khi viết adapter; nếu cần phân biệt NotReady,
Missing, Unavailable, Corrupt, phải review typed error/status contract tương thích riêng.
Không ghi token, bytes PDF, thông tin người mua hoặc đường dẫn private vào log lỗi.

## 6. POS/Admin, Customer và điều kiện bàn giao

POS/Admin dùng chung InvoiceId/snapshot/artifact; quyền tra cứu/in/xuất theo vai trò/phạm vi
riêng, không tái sử dụng quyền Customer và không phát hành/thu lại khi mở tệp. Bản in lại có
dấu Bản sao theo UC-POS-05; không sửa artifact gốc để thêm dấu vào mọi lượt tải Customer.
Tránh đọc nguyên bảng chứng từ từ client để tự quyết định quyền phát hành.

Customer chỉ tải hóa đơn thuộc đơn WEB của mình. Không public locator, không nhận file path,
giữ numeric attachment filename, no-store, Vary Cookie/Authorization, nosniff, CSP sandbox,
DENY framing và no-referrer. Header không thay thế kiểm tra nội dung/kho tin cậy.

Chủ module hóa đơn/POS và người phụ trách đồ án cần bàn giao/chấp thuận:

1. Owner của service phát hành; phân quyền ORIGINAL/ADJUSTMENT, actor/ca, cấp số và snapshot.
2. Chọn kho private, hợp đồng metadata/mapping, renderer/mẫu nội bộ và quy trình publish.
3. Semantics khi chưa archive/missing/corrupt; retention, backup/restore và xử lý orphan.
4. Một tài liệu thật đã phát hành trong môi trường được phép đọc, metadata/hash tương ứng,
   cách truy cập test qua cơ chế secret ngoài Git; không gửi dữ liệu khách thật vào repo/log.
5. Cấp riêng quyền triển khai/SQL fixture nếu cần schema hoặc thử phát hành; không dùng
   yêu cầu lập hợp đồng này làm quyền migration/DML, không sửa fixture lịch sử.

Sau khi có nguồn: triển khai adapter hiện hữu, kiểm chứng bytes/content/hash thật, Customer
A/B, ISSUED/DRAFT/missing, timeout/recovery, cache và POS/Admin dùng cùng chứng từ. Chưa có
đầu vào nên chưa thể báo PASS tích hợp PDF hoặc nghiệm thu UC-WEB-08.

Lượt này chỉ bổ sung tài liệu; không thay code/runtime/schema. Không chạy lại build hoặc
427 tests đã PASS vì không có thay đổi mã thực thi. Kiểm tra whitespace tài liệu và Git diff.
