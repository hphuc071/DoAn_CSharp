# UC-WEB-06 — QR bàn và giao hàng: chuẩn bị chỉ đọc

Ngày 2026-11-07. **PARTIAL; chưa cho phép tạo đơn DINE_IN/DELIVERY.**

## Nguồn và ranh giới

Đã đọc trực tiếp `dac-25-Uc.docx`: UC06 đoạn 384–481; BR-01/02/03/05/08/10/11/12/13/14/16/17/24/29/32/34. BR-01 gọi biểu phí là **đề xuất**; BR-32 cũng gọi hạn 5 phút là đề xuất. Không tự áp dụng hai đề xuất vào cấu hình runtime. Luồng TAKEAWAY nhận sớm nhất giữ DTO, fingerprint, accepted-total, idempotency, BOM, hold, transaction và receipt hiện có.

Schema canonical35 đã có `Ban.MaBamQR binary(32)`, `PhienBanQR > 0`, `QRHetHanLuc` nullable, `DangSuDung`, unique digest/code. Không có bảng phiên QR của Customer. DemoSeed chỉ sinh digest ngẫu nhiên, không cấp secret có thể quét. Chưa có hợp đồng token, công cụ cấp/thu hồi hoặc nguồn routing đang hoạt động. Không suy luận dữ liệu live SQL từ schema trong repo; lượt này không mở SQL.

`DonHang` đã có MaBan/MaBanDaChot, recipient/contact/address, ViDo/KinhDo decimal(9,6), KhoangCachGiaoKm, ChinhSachGiaoHangDaChotJson, HanBaoPhi; không cần tạo DTO order hoặc bảng mới để chứa lại những thông tin này. Những trường đó chưa được tích hợp đầy đủ vào Create DINE_IN/DELIVERY.

## Code triển khai

`POST /api/customer/orders/fulfillment-preview` là thao tác **chỉ đọc**, policy Customer như các API đơn hiện có, response `Cache-Control: no-store`. Không phát operation GUID, không SaveChanges, không tạo payment, audit nghiệp vụ, hold hoặc outbox. Không cấp quyền Create. `CreationEnabled` luôn false. Razor có proxy `POST /Checkout?handler=FulfillmentPreview`, CSRF mặc định của Razor, cookie HttpOnly chuyển thành Bearer ở server; không đưa token xác thực vào JavaScript.

- `ITableQrLookup` / `SqlTableQrLookup`: lookup theo digest, lấy bàn từ server, không nhận mã bàn client. Protocol **đề xuất**, chỉ lookup khi được xác nhận tương thích: canonical base64url của secret ngẫu nhiên 32 byte; SHA256(secret) lưu ở Ban. Digest random của seed không phải QR được cấp. Phải phê duyệt/kiểm thử issuer, entropy, transport không log secret, rotation/revocation bằng digest/version và NULL expiry trước khi mở. Mỗi preview kiểm tra active/digest/version/expiry lại; table deadline NULL chỉ có nghĩa chưa có hạn riêng ở row, preview vẫn có TTL bắt buộc.
- `IDeliveryRouteSource`: nguồn server tin cậy, định tuyến từ điểm quán được cấu hình tới điểm giao khách xác nhận. Runtime chỉ đăng ký `UnavailableDeliveryRouteSource`; không dùng khoảng cách client, đường chim bay hoặc giá trị mẫu. Địa chỉ không xác định/nguồn lỗi hoặc timeout trả lỗi; không báo phí 0 thay thế.
- `DeliveryFeePolicy`: yêu cầu Approval, Version và toàn bộ giá trị cấu hình; tính đồng nguyên, ceil km vượt ngưỡng, phạm vi tối đa và miễn phí theo tiền hàng. Chỉ test mới dùng biểu phí đề xuất. Dữ liệu liên hệ, địa chỉ, tọa độ/độ chính xác kiểm tra trước gọi nguồn. Vị trí khách cung cấp chưa phải bằng chứng tuyến đường; adapter thật phải xác minh khả năng định tuyến.
- `IFulfillmentCartPricer` tái sử dụng calculator menu hiện có để tính tiền hàng; không tạo calculator menu thứ hai. Tách tiền hàng và phí có nguồn. Helper cũ không caller, từng mặc định 2,4km, đã bỏ.
- Data Protection proof chứa owner, hash toàn bộ request (giỏ, token QR hoặc contact/address/coordinate), hash ngữ cảnh bàn/phiên bản hoặc policy/distance, giá và hạn. Không chứa bản rõ tên/địa chỉ/secret. Proof không phải auth token, Idempotency-Key hoặc quyền ghi. Recheck proof sai Customer, đổi giỏ/bàn/địa chỉ/phí/chính sách, sửa proof, hết hạn → 409; rotation/disable QR bị từ chối. Recheck không kéo dài hạn cũ. Hạn lấy min cấu hình preview, nguồn route hoặc QR; kiểm tra lại sau tính giá để không trả preview đã hết hạn.

## Các gate độc lập, tất cả mặc định OFF

| Gate | Phạm vi |
| --- | --- |
| `Checkout:OrderCreationEnabled` | Razor Create TAKEAWAY |
| `Orders:CustomerCreationEnabled` | Backend Customer Create; Web flag không bỏ qua được |
| `Checkout:FulfillmentPreviewEnabled` | Proxy chỉ đọc, từ chối trước outbound HTTP |
| `Orders:FulfillmentPreviewEnabled` | Preview API, từ chối trước lookup/pricing/provider |
| `Orders:TableQrProtocolValidated` | Chỉ cho lookup bằng protocol đã xác minh; không cấp QR hoặc mở Create |
| `Orders:DeliveryPolicy:Approved` | Cần thêm policy đầy đủ và route source thật; không mở Create |

Không dùng Development làm bằng chứng database test hoặc tự bật gate. Không sửa appsettings/.env. `Orders:FulfillmentPreviewLifetimeSeconds` phải cấu hình rõ 1–3600 khi test read preview. Policy gồm Version, BaseKm, BaseFee, PerExtraKm, MaxKm, FreeSubtotal, FreeWithinKm; không có giá trị mặc định. Các cờ approval là điều kiện cấu hình, không thay thế quyết định được phê duyệt hoặc bằng chứng tích hợp.

Backend Create từ chối DINE_IN/DELIVERY (kể cả khác kiểu chữ) trước replay SQL và mọi ghi, dù tất cả preview flags và CustomerCreationEnabled đều true. Không có flag nào mở được hai writer này trong phiên bản này. Quote hàng cũ vẫn chỉ TAKEAWAY; UI tại bàn không còn TABLE-05 giả, delivery fields server-render disabled; thông báo khóa không phụ thuộc JavaScript. Chưa nối UI nhập QR/điểm giao tới proxy read preview; không đặt control có vẻ sẵn sàng khi nguồn runtime chưa có.

## Điều kiện tích hợp tiếp theo

1. Phê duyệt hợp đồng cấp/rotation/revocation QR và kiểm chứng SQL lookup/version, bàn ngừng/hết hạn. Thiết kế handoff qua login không đưa secret vào URL/log/localStorage, đổi bàn có xác nhận, không lộ đơn người cùng bàn. Không gọi QR là xác thực Customer hay bằng chứng hiện diện vật lý.
2. Chọn nguồn routing/store origin và phê duyệt BR-01/32 (fee, radius, expiry). Test lỗi/không xác định điểm giao, boundary km/miễn phí, changed quote và time-of-check/time-of-use bằng nguồn thật được kiểm soát.
3. Thiết kế Create versioned/additive để giữ nguyên hash/replay TAKEAWAY: freeze GUID + original command + accepted total + proof; replay receipt cũ trước kiểm tra proof hết hạn; conflict nếu đổi payload. Proof hiện tại **không được** gắn vào Create cũ rồi coi như đã tích hợp.
4. Trong transaction mới, lock/recheck bàn hoặc quote trước snapshot, kiểm tra giá/BOM/khả dụng, ghi các trường snapshot hiện có, payment/hold/audit/outbox nguyên tử. Không gọi routing ngoài trong transaction dài. Chứng minh quote validity khi commit và replay sau quote hết hạn bằng SQL fixture mới, không ghi vào fixtures lịch sử.
5. Kiểm thử HTTP Customer A/B, forged/expired/revoked QR, table switch, quote changes; SQL create/replay/conflict/stock concurrency/rollback/hold-once; browser refresh/multitab/recovery cho từng hình thức trước mở writer. Hiện chưa có bằng chứng các kịch bản ghi này cho QR/delivery.

Việc đọc preview không sửa thao tác TAKEAWAY chưa rõ kết quả hoặc xóa giỏ. Existing pending GUID/payload được giữ bởi recovery code cũ; preview proof không được dùng thay GUID. SQL/HTTP TAKEAWAY, payment/cancel/outbox trước đây là bằng chứng lịch sử với phạm vi riêng, không suy rộng thành PASS QR/delivery. BR17/EXPIRED, provider VietQR thật và PDF phát hành thật tiếp tục là các gate nghiệm thu độc lập.
