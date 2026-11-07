# UC-WEB-06 Phase 3D.1 Checkout mang đi

> Hardening Backend: route `POST /api/customer/orders` có cờ **riêng ở API** `Orders:CustomerCreationEnabled`, mặc định false và chặn trước đọc SQL/replay. Cờ Web `Checkout:OrderCreationEnabled` không mở Backend. Môi trường test tương lai cần phê duyệt/bật riêng cả hai tiến trình sau guard; không bật từ Development. OFF giữ pending GUID/payload để đối soát/retry khi được phép, không gia hạn hoặc tạo key mới. Báo cáo HTTP lịch sử bên dưới được giữ nguyên, không mô tả cấu hình mặc định hiện hành.

Phạm vi: tạo đơn WEB/TAKEAWAY, nhận sớm nhất, phí giao bằng 0. Chưa hỗ trợ giờ hẹn, kiểm tra giờ hoạt động, QR bàn, giao hàng hoặc thanh toán VietQR thật.

## Feature flag mặc định khóa tạo đơn

Web chỉ cho gửi tạo đơn khi cấu hình server `Checkout:OrderCreationEnabled` có giá trị boolean `true` rõ ràng. Không cấu hình, `false` hoặc giá trị sai đều tắt. Tên biến môi trường tương ứng của process Web là `Checkout__OrderCreationEnabled`. Không tự thiết lập biến này trong phiên làm việc hiện tại và không sửa `.env`, connection string hay Docker. `Development`, URL localhost hoặc port của Web/API không chứng minh database đích an toàn và không tự bật flag.

Khi tắt, server render thông báo khóa và nút disabled ngay trong HTML, kể cả không chạy JavaScript. JavaScript giữ khóa đó cho cả tạo mới và retry; không sinh GUID mới hoặc ghi thêm thao tác. `POST /Checkout?handler=Create` kiểm tra lại flag tại thời điểm xử lý, trả HTTP 503 với code `CHECKOUT_DISABLED` trước xác thực, đọc payload và gọi API. POST thiếu CSRF vẫn bị pipeline Razor từ chối HTTP 400. Kể cả sửa HTML, tự gửi POST có CSRF hợp lệ hoặc dùng trang đã mở lúc flag còn bật, handler vẫn chặn khi cấu hình hiện tại tắt.

Giỏ, báo giá đọc và biên nhận đã lưu vẫn xem được. Tắt flag không tự xóa thao tác chờ, không hủy đơn, không giải phóng kho và không xác định kết quả của yêu cầu đã gửi trước đó. Flag chỉ kiểm soát đường tạo đơn qua Checkout Web; không thay quyền hoặc endpoint Backend của các phân hệ khác. Người vận hành chỉ bật rõ ràng sau khi đã xác minh đúng API/database thử nghiệm và được phê duyệt kiểm thử ghi; bật flag không thay thế việc xác minh đích.

## Luồng và hợp đồng

- Checkout ánh xạ cấu hình giỏ bằng API variants, lấy tổng từ quote và yêu cầu khách xác nhận thông tin/tổng.
- `POST /Checkout?handler=Create` dùng antiforgery mặc định của Razor Pages. Cookie `fourchang_session` là HttpOnly; server chuyển thành Bearer gửi Backend. Backend giữ policy Customer.
- Body Web là `{ operation, acceptedTotal, order, ownerProof }`; `order` giữ DTO `CreateCustomerOrderRequest` hiện tại. `ownerProof` do Data Protection của Web ký/mã hóa theo Customer đã được API xác thực, không phải access token.
- GET Checkout xác minh Customer qua GET profile. Mỗi POST tạo đơn xác minh lại Customer hiện tại với ownerProof trước khi gọi API tạo đơn, kể cả tab cũ còn mở sau đổi tài khoản. Khác chủ/chứng thực thiếu hoặc sai trả 409 CHECKOUT_OWNER_MISMATCH, không gửi tạo đơn. DTO/hash/idempotency Backend không đổi.
- API client gửi `Idempotency-Key` là GUID và `X-Checkout-Accepted-Total` bằng định dạng invariant theo từng request. Trả nguyên HTTP 201 tạo mới hoặc 200 replay cùng biên nhận.
- Header tổng tiền bổ sung cho giao thức Checkout; lời gọi API cũ không có header vẫn tương thích. Backend kiểm tra tổng đã chấp thuận với tổng tính lại sau khóa/replay, trước ghi đơn/kho. Không thay DTO, hash, thuật toán BOM, giữ kho, audit hoặc giao dịch.
- Chênh giá trả 409 với `CHECKOUT_PRICE_CHANGED`, không ghi đơn. Chỉ mã lỗi này cho phép bỏ thao tác và yêu cầu báo giá/chấp thuận mới.
- Không truyền giờ nhận giả. DTO hiện không có trường giờ nhận; tạo mới giữ `ThoiDiemHenNhan = null`. “Nhận sớm nhất” là lựa chọn giao diện hiện tại, không phải thời điểm được Backend cam kết.

## Phục hồi và nhiều tab

Trước gửi, lưu GUID, payload, tổng đã chấp thuận, bản giỏ gốc, mã Customer và ownerProof ở `fourchang.checkout-intent.v1` trong localStorage. Dữ liệu bao gồm tên và số liên hệ để phục hồi đúng payload; không chứa access token hoặc mật khẩu. Dữ liệu được giữ qua reload/đóng tab cho tới xác nhận biên nhận và chọn bắt đầu đơn tiếp theo.

Customer A có thao tác chờ rồi đăng xuất, Customer B đăng nhập: B không được gửi lại, không hiện thông tin người nhận/biên nhận của A trong Checkout, không được dùng nút bắt đầu đơn tiếp theo để dọn thao tác A. GUID/payload/proof của A vẫn giữ nguyên. A đăng nhập lại cùng hồ sơ/origin mới được phục hồi. Thao tác cũ chưa có chủ/proof bị chặn để đối soát, không tự gắn sang Customer hiện tại hoặc tự xóa. Data Protection key ring phải được bảo toàn để đọc proof cũ; không đổi key để xử lý thao tác chưa rõ kết quả. localStorage vẫn là dữ liệu có thể đọc bằng công cụ trình duyệt, nên kiểm tra chủ ở UI không phải mã hóa thông tin người nhận.

Tên và điện thoại lưu dạng rõ, không được mã hóa trong localStorage; cùng hồ sơ trình duyệt và script cùng origin có thể đọc chúng. Payload và bản giỏ cũng có ghi chú khách nhập. HttpOnly chỉ bảo vệ token đăng nhập, không bảo vệ dữ liệu localStorage này. Đăng xuất/đóng tab không xóa bản thao tác; không coi đây là cơ chế lưu dữ liệu riêng tư trên máy dùng chung. Giao diện Checkout có hướng dẫn tương ứng ngay trong trang.

Trên máy dùng chung, dùng hồ sơ trình duyệt riêng được bảo vệ. Nếu đang chờ kết quả, không Clear site data, xóa localStorage, dùng GUID mới hay thử đặt lại ở thiết bị/origin khác. Quay lại đúng tài khoản Customer, đúng hồ sơ và origin để replay khi flag được người vận hành mở an toàn. Khi flag tắt hoặc replay vẫn lỗi, liên hệ người vận hành để đối soát theo mã thao tác; không chia sẻ toàn bộ payload/tên/số điện thoại trong ảnh chụp hoặc log công khai. Chưa có UI tự bỏ khóa thao tác chờ sau đối soát.

Sau biên nhận hợp lệ, có thể bấm “Bắt đầu đơn tiếp theo” để xóa bản thao tác đã hoàn tất, kể cả khi tạo đơn đang khóa; thao tác này không gửi Backend. Kiểm tra và dọn giỏ nháp còn lại theo nhu cầu rồi đăng xuất trước bàn giao máy. Chỉ xóa dữ liệu trang hoặc kết thúc phiên duyệt riêng tư sau khi kết quả đã được xác định; đóng toàn bộ phiên riêng tư lúc đang chờ có thể mất thông tin phục hồi. Không thêm thời hạn tự xóa hay tự xóa khi logout cho thao tác chưa rõ kết quả.

Web Locks khóa thao tác trên cùng origin giữa các tab. Trình duyệt thiếu Web Locks hoặc không lưu được thao tác sẽ chặn gửi. Phải giữ cùng origin trong quá trình phục hồi; không xóa dữ liệu site/khóa thao tác đang chờ khi kết quả chưa rõ. Các trình duyệt, thiết bị và origin khác nhau không chia sẻ thao tác; Backend vẫn chống trùng cho cùng GUID, không suy đoán hai GUID khác nhau là một lần đặt.

Với timeout, mất mạng, 400, 401, 409 khác hoặc biên nhận sai, giỏ và thao tác được giữ. 401 cho liên kết đăng nhập rồi quay lại Checkout; không thay GUID/payload. Các lỗi 400/409 không có mã từ chối chắc chắn vẫn giữ thao tác để tránh tạo trùng khi yêu cầu trước còn đang chạy. Nếu không phục hồi được bằng replay, cần kiểm tra đơn/thao tác với người vận hành; chưa có UI cho phép hủy khóa sau đối soát.

HTTP 502/503 có HTML, rỗng, JSON lỗi hoặc Content-Type không hỗ trợ được báo có kiểm soát theo trạng thái HTTP; không hiển thị HTML lỗi thô, không tạo GUID mới, không xóa giỏ/pending. Chỉ JSON biên nhận hợp lệ mới được chấp nhận. Bộ test không database đã bổ sung các trường hợp gateway này và chuyển Customer A sang B.

Chỉ biên nhận có mã hợp lệ, trạng thái gốc PENDING_PAYMENT, ship=0 và tổng đúng số đã chấp thuận mới được lưu là thành công. Giỏ chỉ được xóa nếu còn trùng bản đã gửi; giỏ đổi ở tab khác được giữ. Biên nhận replay là snapshot lúc tạo; `/Orders` cung cấp trạng thái hiện tại. Nút bắt đầu đơn tiếp theo chỉ mở sau biên nhận hợp lệ. Không đánh dấu PAID hoặc dùng QR minh họa để yêu cầu chuyển tiền.

## Kiểm thử không database

Các test C# mới có trait `Safety=NoDatabase`. HTTP stub và route Playwright thay toàn bộ API. Các kịch bản tạo đơn thành công bật flag rõ ràng chỉ trong cấu hình test giả lập. Test Razor khởi động riêng Web ở Development với API trỏ cổng đóng `127.0.0.1:1`; kiểm tra flag thiếu/tắt/bật và CSRF, không khởi động Backend. Test handler có HTTP stub đếm số lần gọi để chứng minh flag thiếu/tắt/sai không gửi request Backend. Test trình duyệt kiểm tra nút bị sửa thủ công không gửi và thao tác chờ giữ nguyên khi khóa. Playwright dùng Microsoft Edge headless đã cài sẵn.

```powershell
dotnet build FourChang.sln -m:1 /p:UseSharedCompilation=false /nodeReuse:false
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --no-build --no-restore --filter "Safety=NoDatabase"
node --check src/Presentation/FourChang.Web/wwwroot/js/checkout-order.js
node --check src/Presentation/FourChang.Web/wwwroot/js/checkout-quote.js
node --check src/Presentation/FourChang.Web/wwwroot/js/checkout-cart.js
git diff --check
```

Không chạy toàn bộ suite: fixture tích hợp hiện có bootstrap và ghi SQL, nằm ngoài phê duyệt Phase 3D.1.

## Kiểm thử thủ công có ghi dữ liệu — chỉ sau phê duyệt riêng

Chưa thực hiện các bước dưới đây. Trước chạy phải xác minh cấu hình hiệu lực của đúng process Backend trỏ `127.0.0.1:14331 / FourChangPhase3CTest`, không phải SQL chính, và nhận phê duyệt ghi trên đích đó. Không sửa `.env`, chạy init-db, migration, seed, đổi mật khẩu hoặc tự tạo dữ liệu nền.

1. Trên kết nối dự kiến, dùng truy vấn chỉ đọc `SELECT @@SERVERNAME, DB_NAME();` và kiểm tra DataSource của kết nối để xác nhận port 14331 và database FourChangPhase3CTest. Không in connection string có mật khẩu. Xác minh Web trỏ đúng API test, timeout chờ thanh toán đã được cấu hình hợp lệ, và dữ liệu Customer/menu/BOM/kho test đã có sẵn. Chỉ sau phê duyệt ghi riêng mới bật `Checkout:OrderCreationEnabled=true` rõ ràng trên process Web test; không sửa `.env` hoặc mặc định dựa vào Development. Sau kiểm thử, tắt lại flag và giữ thao tác nào còn chưa xác định kết quả.
2. Đăng nhập bằng Customer test, thêm món với đường/đá/topping hợp lệ. Checkout phải mặc định mang đi, chỉ hiện nhận sớm nhất và giá Backend; chưa xác nhận hoặc thông tin sai thì không gửi.
3. Tạo đơn một lần. Kiểm tra HTTP 201, header idempotency tại request Web → Backend, tên/điện thoại snapshot, ship=0, PENDING_PAYMENT, tổng và hạn thanh toán. Đối chiếu chỉ một đơn, khoản giữ, payment intent và audit; không có tiền đã xác minh.
4. Chặn phản hồi sau khi Backend đã commit, tải lại Checkout rồi thử lại. GUID/payload phải giữ nguyên; HTTP 200 phải trả cùng mã đơn, không tăng số đơn/giữ kho. Không xóa localStorage để thử lại.
5. Mở hai tab cùng origin và gửi đồng thời. Chỉ một thao tác được tạo; tab còn lại chờ hoặc hiển thị cùng biên nhận. Sửa giỏ ở tab khác khi request đang chờ rồi xác nhận giỏ sửa vẫn còn sau thành công.
6. Trên môi trường test đã được duyệt, nhờ người có quyền thay giá giữa báo giá và gửi: phải 409 CHECKOUT_PRICE_CHANGED, không tạo đơn/giữ kho; cần báo giá và xác nhận lại. Không tự sửa giá trên SQL chính.
7. Kiểm tra hết phiên, thiếu/sai CSRF, ngắt mạng, 400/409 do cấu hình/tồn và biên nhận sai. Không tạo GUID mới cho thao tác chưa rõ. Kiểm tra luồng hủy/theo dõi đơn hiện có vẫn dùng nguyên backend.

Giới hạn bằng chứng hiện tại: test không database chưa chứng minh rollback/giữ kho/đồng thời SQL trong phiên bản tích hợp. Các phần đó cần test đích 14331 sau phê duyệt riêng. UC-WEB-06 đầy đủ còn cần giờ hoạt động/giờ hẹn, QR bàn thật, vị trí/phí giao đã xác minh, và nối UC-WEB-07 VietQR thật. Endpoint tạo đơn hiện lưu audit; chưa thấy bước ghi outbox và lịch sử chuyển trạng thái riêng như luồng chính của đặc tả. Phase này không bổ sung schema hoặc cơ chế đồng bộ đó.
