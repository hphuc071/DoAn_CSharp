# UC-WEB-06 — Phase 3D.1C, Giai đoạn A

Phạm vi là kiểm thử phục hồi Checkout Web bằng HTTP mock và Edge headless. Không tạo đơn/Customer thật, không khởi động API nối SQL, không fault injection có ghi database. Các test mang trait `Safety=NoDatabase`. Test Razor hiện có chỉ chạy Web độc lập với API trỏ tới cổng đóng `127.0.0.1:1` để kiểm tra CSRF/feature flag.

## Bằng chứng cần kiểm tra

Mock commit ghi nhận command và biên nhận trong bộ nhớ trước khi làm mất phản hồi. Lần thử lại chỉ được trả biên nhận đó nếu command không đổi. Đây là mô phỏng giao thức client, không phải bằng chứng transaction/rollback SQL.

| Tình huống | Điều kiện đạt |
| --- | --- |
| Mock đã commit, mất mạng hoặc timeout | Pending và giỏ còn nguyên; thử lại cùng GUID/payload/tổng tiền dù báo giá mới đã khác; cùng biên nhận, một mock creation |
| Reload lúc request còn chờ | Pending sống qua navigation; request tiếp theo dùng command gốc |
| Đóng/mở lại Edge | Dùng chính profile trên đĩa, không nhập lại localStorage; pending và giỏ được phục hồi, sau đó replay cùng command |
| Hai tab cùng gửi | Web Lock của tab đang gửi ngăn POST từ tab kia; tab kia thấy pending; đóng tab gửi thì tab còn lại có thể replay cùng thao tác |
| Customer A → logout → Customer B | Không nhận quyền sở hữu pending của A; giữ dữ liệu, ẩn thông tin người nhận, không gửi từ UI B; handler kiểm tra cookie/profile B và chặn trước API tạo đơn |
| Flag tắt, pending đang tồn tại | Trang render khóa không gửi POST dù sửa nút; pending giữ nguyên; handler từ chối khi flag đã tắt |
| Giỏ mới được sửa ở tab khác | Biên nhận thao tác cũ không xóa giỏ mới; tab còn lại nhận receipt và không tự gửi tiếp |
| 502/503, HTML/rỗng/unsupported media | Không báo thành công; command/giỏ còn nguyên để thử lại |
| HTTP 200/201 nhưng body sai | Không nhận receipt giả; cùng thao tác có thể phục hồi bằng biên nhận hợp lệ |
| Không lưu được receipt | Không dọn giỏ; bản pending đã lưu vẫn giữ GUID/payload để replay |
| Pending hỏng hoặc thiếu Web Locks | Chặn gửi, không tự xóa pending hoặc tạo thao tác thay thế |

Không đồng nhất POST tới Razor với POST tạo đơn tới Backend. Nếu operator tắt flag sau khi tab đã render enabled, tab đó chưa biết thay đổi và có thể gửi tới Razor. Handler đọc lại flag và trả 503 trước khi gọi Backend. Muốn cập nhật UI, tải lại trang; không có cơ chế push/poll trạng thái flag trong phạm vi hiện tại.

Profile thử nghiệm được xóa sau khi đóng browser. Khởi động lại cùng profile với đóng sạch được kiểm thử; không suy rộng kết quả sang mất điện, browser crash trước khi ghi xuống đĩa, private browsing, dữ liệu site bị xóa hoặc origin/profile khác. Xác thực Customer trong browser được giả lập; cookie/protocol server được kiểm tra bằng HTTP stub, không đăng nhập SQL thật.

## Phục hồi và đối soát thủ công

1. Nếu kết quả chưa rõ, giữ site data và GUID/payload. Không bấm tạo thao tác mới, xóa localStorage, thay tổng tiền hoặc đăng nhập tài khoản khác để gửi tiếp. Tên/điện thoại nằm trong payload lưu tại máy; chỉ xử lý trong phiên của chủ thao tác và tránh chia sẻ ảnh/chụp dữ liệu thô trên máy dùng chung.
2. Khi mạng/phiên/flag đã được khôi phục và chủ thao tác được xác minh, tải lại Checkout cùng origin/profile rồi thử lại cùng command. Phiên hết hạn cần đăng nhập lại đúng Customer; không thay key. Flag bật lại chỉ theo quy trình môi trường được phép.
3. Nếu dữ liệu hỏng/mất, owner proof không giải mã được (ví dụ đổi content root/key ring), bị 409 khác `CHECKOUT_PRICE_CHANGED`, hoặc replay liên tục không có biên nhận hợp lệ: ngừng gửi và liên hệ người vận hành. UI chưa có chức năng tự mở khóa pending sau đối soát.
4. Người vận hành xác minh danh tính Customer, đối chiếu GUID với audit `CREATE_CUSTOMER_ORDER`, fingerprint payload và biên nhận; đối chiếu payment cùng `MaThaoTac`, đơn, trạng thái/hạn hiện tại, BOM và giữ kho bằng SELECT trên môi trường đúng đích. Không dùng access token/mật khẩu trong ticket hoặc log. Nếu không còn GUID, tra theo Customer/khoảng thời gian và mã đơn, không đoán thao tác mới.
5. Có audit/biên nhận hợp lệ: chỉ replay cùng key/payload sau xác nhận quyền sở hữu. Có payment nhưng thiếu audit, hash/owner không khớp, hoặc SQL không nhất quán: chuyển xử lý thủ công, không tự tạo order mới hay sửa audit/kho. Không có bản ghi tại một thời điểm không đủ chứng minh request cũ đã kết thúc; cần xác định request không còn xử lý và kết quả cuối trước khi duyệt thao tác mới.
6. Receipt replay là snapshot khi tạo, có thể chứa `PENDING_PAYMENT` và hạn đã qua. Kiểm tra trạng thái live qua `/Orders`; không coi receipt lịch sử là gia hạn hoặc xác nhận PAID. Chỉ dọn dữ liệu cá nhân trên máy dùng chung khi kết quả đã rõ; không tự xóa pending khi logout.

## Đề xuất integration 14331 — chưa thực hiện, cần phê duyệt riêng

Một kịch bản liên tục với một Customer test, một giỏ nhỏ, một GUID, hai tab cùng origin:

1. Xác minh build/PID/SHA256, Docker project `fourchang-phase3c`, SQL `127.0.0.1:14331 / FourChangPhase3CTest`, Web/API `5380/5381`; từ chối đích 14330. Chỉ cấp timeout 15 phút và flag true trong tiến trình test. Xác minh lại trước mỗi request nghiệp vụ.
2. SELECT baseline số đơn/thanh toán/audit/giữ kho, BOM và TonKho. Xác nhận không còn pending chưa đối soát của Customer. Không đăng ký/seed/migration nếu chưa có phê duyệt riêng.
3. Gửi lần đầu từ tab A; giữ response ở lớp test transport SAU KHI đã nhận HTTP 201 từ Backend và đã SELECT xác nhận commit. Không chỉnh business code hoặc transaction để tạo lỗi. Cho tab B thử khi Web Lock A còn giữ: không được có POST tạo đơn thứ hai. Sau đó làm mất response tới browser để client giữ pending.
4. Chụp SELECT sau commit: đúng một đơn, một payment PENDING, một audit tạo đơn, BOM/giữ kho đúng, verified=0. Reload tab A hoặc đóng tab A rồi retry từ tab B bằng chính GUID/payload/tổng tiền gốc.
5. Chỉ một POST replay, HTTP 200 cùng mã/hạn/biên nhận; SELECT sau replay phải bằng sau commit. Giỏ chỉ dọn khi receipt hợp lệ; giỏ mới ở tab khác phải được giữ. Không điều hướng thanh toán thật.
6. Dừng nếu HTTP/SQL lệch kỳ vọng; không GUID mới hoặc retry tự động. Dừng tiến trình test, giữ bằng chứng đã che thông tin nhạy cảm và pending nếu kết quả chưa rõ. Không xóa dữ liệu để làm sạch test.

Kịch bản này cần quyền tạo đúng một đơn mới và fault injection ở transport sau commit; **chưa được phép chạy trong Giai đoạn A**. Nó không chứng minh mọi concurrency SQL, crash recovery hoặc thanh toán thật.

## Kết quả Giai đoạn A

Build `FourChang.sln` đạt, 0 warning/error. Bộ `Safety=NoDatabase` đạt **70/70**, 0 skipped (tăng 15 ca so với 55 ca trước). JavaScript syntax checks cho checkout-order.js, checkout-quote.js, checkout-cart.js đạt. Log TRX nằm tại `artifacts/phase3d1c/test-results/phase3d1c.trx`.

`git diff --check` đã đạt trước kiểm tra cuối; sau đó xuất hiện thay đổi ngoài lượt chỉnh sửa này tại file đang mở `Pages/Orders/Index.cshtml.cs`: thêm một dòng trống cuối file, khiến check toàn repository báo `179: new blank line at EOF`. Giữ nguyên thay đổi song song này; kiểm tra diff loại riêng file đó và các file mới của Phase 3D.1C không có lỗi whitespace. Chưa xử lý dòng trống thuộc file ngoài phạm vi.

Lượt chạy đầu 69/70 do đoạn test khôi phục `Storage.prototype.setItem` trả về hàm khiến Playwright tự gọi sai `this`; đã sửa thành callback không trả về hàm và chạy lại toàn bộ. Không phát hiện lỗi Checkout cần sửa trong các trường hợp đã mô phỏng; không thay source nghiệp vụ, context hoặc `.env`.

Các file bổ sung/chỉnh sửa trong lượt này: `CheckoutBrowserTests.cs` (dùng chung routing mock cho browser restart, tăng assertion hai tab/gateway), `CheckoutRecoveryBrowserTests.cs` (14 ca browser mới), `CheckoutProxyTests.cs` (một ca mất phản hồi sau mock commit, xác minh token B trong ca stale tab), và tài liệu này. Chỉ chạy kiểm thử không ghi database và dừng tại Giai đoạn A.

## Chuẩn bị Giai đoạn B — chưa có quyền chạy nghiệp vụ

Lượt chuẩn bị chỉ rà soát source, build, chạy lại `Safety=NoDatabase` và kiểm tra guard bằng dữ liệu giả. Không khởi động API nối SQL, không mở kết nối SQL, không kiểm tra tài khoản thật, không đăng nhập/đăng ký, không chạy lại các harness controlled/replay của phase trước. Các POST nội bộ của test là mock hoặc Razor test trỏ Backend tới cổng đóng.

### Review bằng chứng và diff

70 ca chia thành 31 `CheckoutBrowserTests`, 30 `CheckoutProxyTests`, 9 `CheckoutDeadlineTests`. Browser thực thi JavaScript production nhưng thay HTTP bằng routes mock; proxy thực thi Razor handler với HttpMessageHandler giả, chỉ test CSRF/flag cũ chạy Web độc lập trỏ API đóng. Không gọi `TestApi.Start`/bootstrap trong bộ này; chỉ dùng hàm tìm repository root.

- Nhiều tab: `TwoTabsCannotSubmitDifferentOperationsWhileFirstIsInFlight` giữ response để Web Lock còn thuộc tab A, kiểm tra không có POST thứ hai và receipt được tab B nhận qua storage; `OtherTabCanRecoverSameCommitAfterSendingTabCloses` kiểm tra chuyển quyền retry sau khi tab gửi đóng.
- Mất phản hồi: `LostResponseAfterMockCommitRetriesOriginalEvenIfQuoteChanges` lưu receipt trong bộ nhớ rồi abort/timeout, thay báo giá nhưng giữ command cũ; proxy `MockCommitThenLostResponseReplaysSameBackendKeyBodyTotalAndReceipt` kiểm tra header Idempotency-Key, accepted-total, body và token server.
- Giỏ thay đổi: `NewNonemptyCartFromOtherTabSurvivesOriginalReplayReceipt` giữ giỏ quantity=3 sau receipt cũ; không tự gửi đơn tiếp theo.
- Đổi tài khoản: browser giả lập owner A/logout/B, giữ pending và ẩn tên; proxy stale tab dùng cookie B thực sự trong request giả, profile B dẫn tới 409 và 0 request API tạo đơn. Đây không phải test đăng xuất/đăng nhập thật với SQL.
- Profile browser đóng sạch rồi mở lại được kiểm tra bằng đúng profile trên đĩa; không suy ra crash/mất điện hoặc dữ liệu site đã xóa có thể tự phục hồi.

Đầu lượt chuẩn bị, thay đổi riêng của `Pages/Orders/Index.cshtml.cs` chỉ thêm dòng trống sau dấu `}` cuối, Git báo `179: new blank line at EOF`; không phải lỗi cú pháp hay thay logic. File không có trong snapshot đầu Phase 3D.1C và nằm ngoài các chỉnh sửa Checkout đã thực hiện. Không đủ bằng chứng xác định tác giả; không sửa file đó. Trong lúc review xuất hiện thay đổi song song đưa file về trùng HEAD; kiểm tra cuối toàn repo và danh sách đường dẫn Checkout riêng đều đạt. Đây không phải thao tác sửa Orders của lượt chuẩn bị này.

Diff review phải gồm cả bốn file mới: tài liệu này, `CheckoutRecoveryBrowserTests.cs`, `CheckoutDeadlineAssertion.cs`, `CheckoutDeadlineTests.cs`. Đầu lượt bốn file còn untracked và đã có đủ trong full-review.diff cũ. Trong lúc review, chúng chuyển sang intent-to-add bằng thay đổi song song, nội dung staged vẫn rỗng; không chạy `git add -N` trong lượt chuẩn bị này. Patch hiện tại lấy từ `git diff` đã gồm cả bốn file; chỉ cần cộng `git diff --no-index -- NUL <file>` nếu file vẫn untracked. Manifest đường dẫn/SHA256 kiểm tra phần thêm của mỗi patch khớp toàn bộ nội dung file. Bằng chứng mới nằm dưới `artifacts/phase3d1c-preparation/` (Git ignore).

### Giới hạn cho một lượt Giai đoạn B dự kiến

| Loại | Giới hạn toàn lượt |
| --- | ---: |
| Customer test có sẵn được dùng | 1 |
| GUID/thao tác mới | 1, do browser tạo và lưu bền vững trước request đầu |
| Đơn/payment/audit tạo đơn mới | tối đa 1 mỗi loại, dự kiến đúng 1 nếu tạo thành công |
| POST Web `handler=Create` | tối đa 2: một tạo, một replay |
| POST API `/api/customer/orders` | tối đa 2, tương ứng hai request Web trên |
| Fault injection | 1, chỉ response tạo đầu sau commit đã được chứng minh |
| POST quote chỉ đọc | tối đa 8 mỗi hop, phục vụ tự báo giá của hai tab/reload/sự kiện giỏ |
| GET cho preflight/profile/menu/variants/Checkout/health | tối đa 60 toàn harness; hết ngân sách thì dừng |
| Đăng ký/login/logout, hủy đơn, review hết hạn, thanh toán, SQL DML trực tiếp | 0 |

Không reuse GUID của đơn 7 để tạo đơn mới. Chỉ khi được phê duyệt B, dùng browser profile riêng cùng origin 5380, xác nhận không có pending chưa rõ rồi để browser tạo GUID mới duy nhất; trước POST kiểm tra key chưa có trong audit/payment bằng SELECT. Nếu phiên Customer có sẵn hết hạn, dừng xin duyệt đăng nhập riêng, không tự cấp phiên mới. Tên/điện thoại chỉ là dữ liệu test. Không đánh dấu PAID hoặc tạo thanh toán thật.

### Thứ tự thực hiện sau khi được duyệt

1. Build hiện tại, giữ SHA256, sao chép output riêng; tạo API/Web process con sở hữu được PID, chỉ bind `127.0.0.1:5381/5380`. API env riêng có connection override test cùng `FOURCHANG_SQL_SERVER=127.0.0.1,14331`, `FOURCHANG_DATABASE=FourChangPhase3CTest`, timeout 15 phút. Web env riêng có API URL 5381 và flag true. Không dùng flag mặc định, Development hay tên file cấu hình để suy ra test DB. Giữ đúng spelling content root/key ring để owner proof hợp lệ.
2. Kiểm tra Docker project/container/port; SELECT identity trên test connection và đối chiếu hostname container, schema, phiên/profile Customer; xác minh PID/hash/listener riêng. Đồng thời bắt buộc có socket SQL test thuộc đúng PID API. Chưa đủ bằng chứng thì không mở cổng gửi POST.
3. SELECT baseline B0: global và theo Customer/order/key của DonHang, ThanhToan, NhatKyHeThong (tách CREATE_CUSTOMER_ORDER), GiuChoNguyenLieu; snapshot công thức/BOM nguyên liệu và TonKho gồm tồn thực, tổng giữ, lượng đã dùng/giải phóng, trạng thái/hạn. Lưu intent/command/cartRaw và accepted total để đối chiếu, bảo vệ payload/token, log chỉ metadata đã che.
4. Tab A gửi lần đầu. Browser route chặn đúng URL/method Create và đúng key/command. Dùng `route.FetchAsync` với `MaxRetries=0`, `MaxRedirects=0` để chuyển đúng request đó tới Web một lần; KHÔNG vừa fetch vừa continue request. Web bình thường gọi API; không chèn delay/commit hook vào code ứng dụng.
5. Chỉ sau khi upstream Web trả HTTP 201 với receipt hợp lệ, SELECT B1 phải chứng minh cùng key/Customer đã có đúng đơn/payment/audit và giữ kho mong đợi. Source hiện tại commit transaction trước khi trả 201. Nếu không có HTTP 201 hoặc chưa chứng minh commit, dừng ở kết quả chưa rõ; không coi timeout là bằng chứng đã commit.
6. Trong khi response tới tab A vẫn đang giữ và Web Lock còn giữ, tab B cùng browser/profile thử gửi: phải thấy pending và không có POST Create thứ hai. Sửa giỏ ở B thành một giỏ khác không rỗng để kiểm tra không xóa nhầm. Bước kiểm tra sau upstream response có ngân sách 10 giây; nếu vượt hoặc client timeout sớm, dừng đối soát, không tự chạy tiếp.
7. Đúng một lần `route.AbortAsync` làm mất response tới tab A, không gửi body receipt. Xác nhận pending vẫn có GUID/command/total gốc, chưa có receipt, giỏ mới vẫn còn. Reload tab A cùng browser để kiểm tra pending tồn tại, không nhấn tạo/retry ở A. Chỉ tab B được nhấn retry sau khi A đã nhả Web Lock; Web POST thứ hai phải khớp command/key/total/Customer gốc.
8. Replay không bị injection, HTTP 200 phải trả đúng code/ID/amount/deadline từ B1; không tính lại hạn. SELECT B2 phải bằng B1 ở số lượng, payment state/verified=0, audit, BOM, mọi dòng giữ kho và TonKho; B1 so B0 chỉ tăng đúng một đơn/payment/audit và lượng giữ theo BOM. Giỏ mới phải còn; pending lưu receipt sau khi hợp lệ; hai tab không tự tạo thêm request.
9. Dừng ngay khi HTTP/SQL/owner/key/hash/giới hạn lệch kỳ vọng, đóng toàn bộ cổng POST, hủy tasks, đóng browser, dừng chỉ các API/Web do harness tạo; giữ durable marker, intent/receipt/log đã che và snapshots. Không auto retry, key mới, xóa dữ liệu, init-db/migration/seed hay xóa Docker volume. Dừng sau lượt tối thiểu để review, kể cả khi PASS.

### Chặn SQL chính và phạm vi fault injection

`DatabaseSettings.ConnectionString()` ưu tiên `ConnectionStrings__FourChang`. `LocalEnvironment.Load()` không ghi đè biến đã tồn tại; vì vậy phải truyền connection override đúng đích trước khởi động child API và từ chối missing/empty override thay vì rơi về `.env` có default SQL chính. Kiểm tra cấu hình chưa đủ thay cho bằng chứng process thực tế.

Guard thuần ở `artifacts/phase3d1c-preparation/StageB.Safety.ps1` có self-test offline `Test-StageBSafety.ps1`: whitelist chính xác server, database, app login, env và Web URL/flag; kiểm tra socket Established của đúng API PID, loại kết nối vào listener 5381. Bất kỳ outbound khác test `127.0.0.1:14331`, gồm 14330, database chính, PID khác, thiếu socket evidence đều ném lỗi và gọi callback dừng toàn lượt trước khi cho request tiếp tục. Không in connection string hoặc credential. Self-test chỉ dùng chuỗi/socket giả; không đọc `.env`, mở SQL, chạy Docker hoặc dừng process thật.

Runner B tương lai phải nối callback này với một cancellation token/cổng POST chung cho cả hai tab và dừng các process sở hữu trong `finally`; không chỉ đánh dấu một assertion thất bại rồi cho tab kia tiếp tục. Chạy gate sau health/profile, ngay trước mỗi POST Create và giám sát socket/config/hash trong toàn lượt. Nếu công cụ không nhìn thấy socket đúng API PID thì **dừng trước POST**, không thay bằng bằng chứng GET nonce (GET nonce chỉ chứng minh listener, không chứng minh đích SQL). Không kill tiến trình lạ. Watcher có độ trễ lấy mẫu nên không thay cho việc pin connection/config và guard trước POST.

Hiện mới chứng minh guard bằng dữ liệu giả, chưa nối/thực thi runner B hoặc xác minh SQL/PID thật trong lượt chuẩn bị. Cần hoàn tất wiring và preflight runtime trong lượt B được duyệt trước khi mở cổng POST. Không được báo guard offline là đã chặn một kết nối production thật.

Fault injection chỉ tồn tại ở route của browser harness cục bộ dưới `artifacts/`, áp dụng một lần cho origin 5380 và command được pin sau khi guard đạt. Không thêm middleware/header/endpoint/feature flag gây lỗi vào production Web/API, không sửa timeout client production hoặc logic tài chính/transaction. Khi bỏ harness route, hành vi ứng dụng không đổi; DLL production không chứa cơ chế injection này.
