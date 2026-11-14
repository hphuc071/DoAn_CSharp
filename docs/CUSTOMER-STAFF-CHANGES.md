# Tổng hợp thay đổi Khách hàng và Nhân viên

Ngày đối chiếu: 13/11/2026. Phạm vi: 8 UC-WEB và 5 UC-POS trong `D:/8.tailieu/dac-25-Uc.docx`; code tại `D:/2.C#C++/C #/DoAnCSharp`; các nhánh GitHub của nhóm. Không đặc tả chức năng quản trị. Việc duyệt hủy chỉ được nhắc tại điểm giao tiếp cần thiết với Nhân viên và Khách hàng.

Đây là bản đối chiếu, không phải xác nhận mọi yêu cầu đã hoàn tất hoặc đã push. Yêu cầu mới nhất được phân biệt với hiện trạng code. Không sửa nghiệp vụ hoặc chạy lại toàn bộ bộ test trong lượt tổng hợp này.

## 1. Phiên bản được đối chiếu

- GitHub: https://github.com/hphuc071/DoAn_CSharp
- GitHub `main` và HEAD local cùng commit `7431df11bc9dac42ec07fcbc78309e79359c467a` — “hoàn thành khách hàng”.
- Working tree local có nhiều file đã sửa và file mới chưa commit. Vì vậy cùng HEAD không có nghĩa nội dung local đã đồng bộ với GitHub.
- So sánh trực tiếp các đầu nhánh trên GitHub với `main` tại thời điểm kiểm tra:

| Nhánh | Kết quả đối chiếu |
|---|---|
| `feature/adm01-05-spec25` | Cùng commit với main. |
| `feature/nhan-vien` | Không có commit riêng; main đi trước 2 commit. |
| `feature/tai-menu-cart-order` | Không có commit riêng; main đi trước 37 commit. |
| `tv3_webui` | Không có commit riêng; main đi trước 47 commit. |
| `feat/thai-web-cart-order` | Phân kỳ: có 8 commit riêng, thiếu 3 commit của main. |
| `feature/QuanLy` | Phân kỳ: có 1 commit riêng, thiếu 41 commit của main; ngoài phạm vi nghiệp vụ của báo cáo này. |

Nhánh `feat/thai-web-cart-order` còn các commit về giỏ/checkout, polling đơn, đặt mang đi có hẹn giờ, QR bàn và tạo đơn tại bàn có kiểm thử SQL. Không kết luận các cách triển khai này đã được tích hợp vào working tree hiện tại, cũng không đề nghị gộp nguyên nhánh mà chưa đối chiếu xung đột với luồng mô phỏng mới.

## 2. Các UC Khách hàng

| Mã và tên UC theo Word | Thay đổi hoặc quy tắc hiện tại | Đối chiếu triển khai |
|---|---|---|
| UC-WEB-01 Đăng ký tài khoản | Số điện thoại bắt buộc đúng 10 chữ số; yêu cầu mới là mật khẩu tối thiểu 5 ký tự. Email tùy chọn. | HTML có giới hạn điện thoại và hướng dẫn 5 ký tự. Tuy nhiên `Register.cshtml.cs` vẫn đặt `MinimumLength = 10`; chưa đồng bộ hoàn toàn. |
| UC-WEB-02 Đăng nhập và đăng xuất | Khách dùng tài khoản riêng; chỉ xem/thao tác dữ liệu của mình. Giữ xử lý phiên và luồng đổi mật khẩu tạm. | Có các luồng đăng nhập, đăng xuất và đổi mật khẩu tạm. `ChangeTemporaryPassword.cshtml.cs` vẫn yêu cầu tối thiểu 10 ký tự, khác yêu cầu 5 ký tự. |
| UC-WEB-03 Quản lý thông tin cá nhân | Giữ hồ sơ và sổ địa chỉ. Không yêu cầu khách tự nhập vĩ độ/kinh độ trên giao diện; điện thoại đúng 10 chữ số. | Tọa độ được ẩn bằng input hidden trên trang hồ sơ; không phải xóa cột dữ liệu. Địa chỉ giao đã chốt trong đơn không đổi theo hồ sơ sửa sau này. |
| UC-WEB-04 Duyệt và tra cứu thực đơn | Giữ thương hiệu nâu/kem, trình bày theo hướng quán cà phê; tìm món, danh mục và còn/hết. Tắt bán hiển thị Tạm hết, khóa mua; chủ động ẩn thì mới bỏ khỏi danh sách. | Giá niêm yết lấy từ cấu hình do API chọn, không phải một giá cố định cho mọi size. API và cập nhật nền ưu tiên cấu hình mặc định còn hàng; form mở cùng cấu hình đó. |
| UC-WEB-05 Tùy chỉnh món và quản lý giỏ hàng | Tách size, nóng/lạnh, đường và đá. Nóng chỉ hiện đường; lạnh có đường và đá. Có topping, số ly, ghi chú; hai lựa chọn Thêm vào giỏ và Thanh toán. | Thêm giỏ xong đóng/dọn form để chọn món mới, vẫn giữ các dòng đã thêm. Thanh toán đưa đến checkout. Cấu hình và giá lấy từ API; sửa giỏ giữ cấu hình cũ nếu còn hợp lệ. |
| UC-WEB-06 Đặt món đa hình thức | Mang đi; tại bàn chọn từ danh sách; giao tận nơi mô phỏng phí cố định 15.000đ/đơn. Không bắt khách nhập khoảng cách hay tọa độ. | Tại bàn/giao hàng mô phỏng chỉ bật khi có cờ và môi trường Development. Không coi đây là định vị, tính tuyến đường hay triển khai giao hàng thật. Checkout kiểm lại cấu hình, giá, bàn/địa chỉ và nguyên liệu trên server. |
| UC-WEB-07 Thanh toán chuyển khoản VietQR | Web trả trước bằng chuyển khoản. Có QR mô phỏng cho đồ án; trạng thái tự cập nhật sau khi server xác nhận. Sau thu đủ, phiên checkout được dọn để đặt đơn mới. | Mở/quét QR chỉ mở trang mô phỏng; GET không ghi nhận tiền. Bước xác nhận mô phỏng POST mới cập nhật. Mô phỏng bị chặn ngoài Development. Thay ảnh QR bằng QR cá nhân chưa đủ để xác minh chuyển khoản thật. |
| UC-WEB-08 Quản lý đơn hàng cá nhân | Mã dễ đọc WEB-ID; xem danh sách/chi tiết, món và thanh toán/hóa đơn. Khách gửi yêu cầu hủy thay vì hủy ngay; sau duyệt và còn khoản được hoàn tại quầy thì hiện hướng dẫn đến quầy. | Chỉ đơn của chính khách đủ điều kiện mới gửi hủy; thời gian chờ duyệt chưa hủy đơn. Sau khi đã chi tiền, thông báo nhận hoàn biến mất. Có EXPIRED riêng với CANCELLED. Trang Web chi tiết vẫn còn phần hóa đơn/đối soát, chưa đồng nhất hoàn toàn với bố cục 4 phần của Desktop. |

### Cấu hình và giá món

- Cà phê sữa, Bạc xỉu, Latte, Matcha latte có Nóng/Lạnh theo dữ liệu cấu hình hiện có; Trà đào và Cold brew chỉ có Lạnh. Không tự thêm biến thể hoặc BOM chưa được khai báo.
- Đơn giá một ly = giá cấu hình được chọn + tổng giá topping nhân số phần trên ly.
- Thành tiền dòng = đơn giá một ly nhân số ly. Tổng đơn = tổng các dòng hiệu lực + phí giao đã chốt.
- Bạc xỉu M lạnh 39.000đ và S nóng 34.000đ là hai cấu hình khác nhau. Lỗi trước đây là thẻ hiện giá M lạnh nhưng POS tự mở S nóng. Form nay chọn đúng mã cấu hình đang niêm yết; đổi cấu hình thì giá thay đổi theo.
- Không đổi giá database để ép các size bằng nhau, không tính lại giá đơn đã chốt theo menu mới.

### Hành trình đặt món hiện tại

1. Khách chọn món và cấu hình hợp lệ.
2. Thêm vào giỏ để chọn tiếp, hoặc chọn Thanh toán để sang checkout; giỏ cũng có nút đi checkout.
3. Kiểm tra hình thức nhận, người nhận, điện thoại, bàn hoặc địa chỉ và giá đã báo.
4. Tạo đơn chờ thanh toán, giữ nguyên mã thao tác khi chưa rõ kết quả; không bấm lại để tạo đơn khác.
5. Sang trang thanh toán, xác nhận mô phỏng khi dùng môi trường đồ án; server cập nhật trạng thái.
6. Checkout dọn phiên cũ sau thu đủ; không xóa giỏ mới mà khách đã sửa ở tab khác. Đơn cũ vẫn ở lịch sử.

## 3. Các UC Nhân viên

| Mã và tên UC theo Word | Thay đổi hoặc quy tắc hiện tại | Đối chiếu triển khai |
|---|---|---|
| UC-POS-01 Bắt đầu và kết thúc ca làm việc | Không đăng nhập tài khoản POS; nhập mã nhân viên, họ tên, ca và tiền đầu ca. Mỗi nhân viên được làm Sáng/Chiều/Tối mỗi loại một lần trong cùng ngày UTC+07. Không ép giờ hoặc thứ tự ca. | Xác minh nhân viên ACTIVE; dropdown chỉ còn ca chưa làm. Server chặn lặp kể cả đổi máy, ca trước đã đóng/khóa và hai máy gửi đồng thời. Gửi lại đúng mã thao tác trả cùng ca. Ngày mới được chọn lại. |
| UC-POS-02 Tạo đơn tại quầy | Chọn nhiều món, mỗi dòng có cấu hình riêng; sửa/xóa dòng nháp. Size, nóng/lạnh, đường/đá và topping tách riêng như Web. Bỏ ô Khách đưa và tiền thừa, hiển thị tổng tiền. | Cấu hình ban đầu khớp giá thẻ. API kiểm lại giá, nguyên liệu và phiên ca. Tiền mặt gửi đúng số phải thu; không thu tiền chỉ vì xem giỏ. Thanh toán thành công tạo chứng từ theo luồng checkout. |
| UC-POS-03 Tiếp nhận và xử lý đơn hàng | Nhân viên được tiếp nhận, bắt đầu pha, pha xong và bàn giao/hoàn tất theo trạng thái. Không nhập lý do cho bốn bước thường lệ này. Theo dõi số ly thực của từng dòng. | Số ly gợi ý là phần còn phải xử lý của dòng; không tự coi toàn đơn đã pha khi mới xử lý một dòng. Có nút bước tiếp theo và menu thao tác phụ. Đơn được tách theo ca hiện tại/nhận từ ca trước; cập nhật nền giữ lựa chọn và vị trí cuộn. |
| UC-POS-04 Hủy món trước pha chế | Nhân viên gửi yêu cầu hủy phần chưa pha để chờ duyệt; không tự hủy thành công. Đơn hủy có trang riêng. Yêu cầu của khách cần Nhân viên chuyển duyệt; yêu cầu Nhân viên tự gửi đã ở bước chờ duyệt. | Gửi yêu cầu chưa đổi số ly hủy, kho hoặc tiền. Trong lúc chờ chặn xử lý đơn để tránh vừa pha vừa duyệt. Duyệt mới hủy và lập hạn mức hoàn; từ chối giữ đơn. Chi tiền thực tế nằm ở Hóa đơn → Hoàn tiền tại quầy. |
| UC-POS-05 Thanh toán và phát hành hóa đơn | POS có tiền mặt hoặc QR mô phỏng. Hóa đơn có tìm kiếm mã hóa đơn/mã đơn/tên khách, lọc ngày và phân trang. Chi tiết được trình bày theo thông tin quán, hóa đơn, khách, món và thanh toán. | Thu đủ mới phát hành theo luồng checkout; đơn Web đã trả không bị thu lại. Hóa đơn đọc giá đã chốt. Hoàn tại quầy chỉ khi khoản được duyệt, ca mở, đúng người nhận và đủ quỹ; xác nhận đã giao/khách nhận mới ghi chi thành công. |

### Kết thúc ca

- Đã bỏ chức năng Nhân viên Tạm khóa ca, Đề nghị tiếp tục ca và ô lý do chênh lệch.
- Nhân viên nhập tiền mặt cuối ca sau kiểm đếm. Số này phải khớp số hệ thống trước khi kết thúc bình thường.
- Tiền mặt cần kiểm đếm = tiền đầu ca + thu CASH đã xác minh của chính ca − hoàn CASH đã chi thành công của chính ca. Chuyển khoản không cộng vào quỹ tiền mặt.
- Thống kê doanh số chỉ hiện tổng tiền đơn COMPLETED và số đơn COMPLETED; bỏ Tiền mặt thuần từ đơn và các dòng thống kê tiền chi tiết trước đây. Khối kiểm đếm cuối ca vẫn tồn tại.
- Tổng tiền đơn hoàn thành gồm giá trị hiệu lực và phí theo đơn; đây không phải số tiền mặt bắt buộc nằm trong két.
- Nút Gửi kết quả & kết thúc ca lưu báo cáo/kết thúc đúng ca khi đủ điều kiện rồi quay về mở ca. Ca còn đơn dở/khoản chưa rõ vẫn phải giải quyết hoặc bàn giao, không tự coi đã xong.
- Phiên đã lưu hợp lệ vẫn có thể tiếp tục đúng ca; không phải tạo thêm ca cùng tên. Mã phục hồi một lần chỉ cấp quyền đối chiếu, không mở quyền bán.

### Phân biệt đơn ca trước và ca sau

- Mỗi ca có mã riêng; mỗi giao dịch thu/hoàn giữ nguyên ca phát sinh.
- Danh sách POS có Ca này/đơn Web chờ nhận, Đơn nhận từ ca trước và Tất cả ca.
- Đơn Web được tiếp nhận sẽ gắn ca xử lý nếu chưa có; không đổi ca nguồn của tiền đã thu.
- Chỉ các đơn trong danh sách bàn giao trực tiếp được đưa sang nhóm nhận từ ca trước; không kéo mọi đơn của các ca tổ tiên.
- Đơn hoàn thành được tính vào ca hoàn thành theo audit thao tác, có fallback cho dữ liệu cũ. Đơn dở/hủy không cộng doanh số hoàn thành. Không thu tiền thêm lần nữa khi đổi ca.

## 4. Đặc tả và BR cần cập nhật

| Quy tắc gốc | Nội dung cần thay đổi hoặc làm rõ |
|---|---|
| BR-01 và BR-32 Phí giao/địa chỉ/báo giá | Bổ sung chế độ đồ án: giao hàng cố định 15.000đ, không tính tuyến đường; không áp dụng công thức khoảng cách/miễn phí của bản gốc cho mô phỏng. Không trình bày chế độ này như dịch vụ giao thật. |
| BR-03 Còn hết và ẩn món | Tắt bán hiển thị Tạm hết; chỉ bỏ công bố/ẩn chủ động mới bỏ khỏi danh sách. Khả dụng vẫn phụ thuộc tổ hợp/BOM/nguyên liệu. |
| BR-04 Hủy trước pha | Thay quyền tự hủy của Nhân viên bằng gửi yêu cầu chờ duyệt. Tách trạng thái yêu cầu khỏi trạng thái đơn; gửi thành công không có nghĩa hủy thành công. |
| BR-07 Mật khẩu | Chốt tối thiểu 5 ký tự; cần thống nhất cả validation máy chủ Web, không chỉ HTML/hướng dẫn. |
| BR-09 và BR-33 Ca làm việc | Thay giới hạn tối đa một ca chưa kết thúc bằng mỗi loại Sáng/Chiều/Tối một lần/ngày/nhân viên; không ràng buộc giờ thực tế hoặc khóa theo máy. Bỏ tạm khóa/đề nghị tiếp tục do Nhân viên chủ động; giữ khôi phục sự cố có kiểm soát. |
| BR-10 QR bàn | Chế độ mô phỏng cho chọn bàn hoạt động từ danh sách; QR bàn thật là một cách triển khai khác, chưa được coi đã tích hợp từ nhánh Web riêng. |
| BR-11, BR-12 và BR-13 Cấu hình/giá/snapshot | Tách điều khiển size/nhiệt độ/đường/đá; nóng ẩn đá. Thẻ món và form dùng cùng cấu hình khởi tạo. Công thức tính tiền và snapshot giữ nguyên. |
| BR-15 và BR-16 Vòng đời/thu tiền | Đơn POS checkout tiền mặt hiện chuyển ACCEPTED sau thu, thay cách tách gửi Chờ xác nhận rồi thu trong Word. QR bắt đầu PENDING_PAYMENT; Web đủ tiền mới chờ tiếp nhận. Không cộng doanh số hoàn thành trước COMPLETED. |
| BR-17 Hết hạn | Code có EXPIRED riêng, không còn ghi là “chưa chốt mã trạng thái”. Không gộp với khách chủ động hủy. |
| BR-18 Chuyển khoản | Bổ sung môi trường mô phỏng: mở QR không ghi tiền; bước xác nhận mô phỏng mới cập nhật. Luồng ngân hàng thật vẫn cần nguồn xác minh đáng tin cậy. |
| BR-19 Đối chiếu tiền mặt | Bỏ yêu cầu nhập lý do chênh trong UI; lệch thì chặn kết ca bình thường. Giữ công thức quỹ theo giao dịch CASH. Bổ sung hai thống kê doanh số/số đơn COMPLETED tách khỏi kiểm đếm quỹ. |
| BR-20 Hóa đơn | Luồng checkout phát hành tự động khi đủ điều kiện; xem lại dùng snapshot. Tìm kiếm/lọc ngày và chi tiết dễ đọc không cho phép sửa hóa đơn gốc. |
| BR-21 Hoàn tiền | Nhân viên được chi CASH tại quầy theo khoản đã duyệt, kiểm người nhận/quỹ/ca; chỉ khi giao tiền thật mới ghi thành công. Duyệt không đồng nghĩa đã chi. |
| BR-34 Đổi ý/sửa đơn | Nháp được sửa; đơn đã gửi không tự viết lại snapshot. Không coi chức năng tạo đơn bổ sung có liên kết trong Word là hoàn tất chỉ từ việc POS thêm được nhiều món vào nháp. |

Giữ các nguyên tắc không đổi: quyền/sở hữu kiểm tại API; tiền/kho thực hiện nguyên tử, mã chống trùng và version; món đã chốt không đổi giá khi menu đổi; không hoàn nguyên liệu đã pha chỉ vì hoàn tiền; không bỏ lịch sử giao dịch khi chuyển ca.

## 5. Các điểm chưa đồng bộ và giới hạn

1. **Mật khẩu 5 ký tự chưa được áp dụng đầy đủ trên Web.** `Pages/Account/Register.cshtml.cs:15` và `ChangeTemporaryPassword.cshtml.cs:15` còn `MinimumLength = 10`. Cần sửa và test chuỗi 5–9 ký tự; báo cáo này chỉ ghi nhận, không tự sửa code ngoài phạm vi tổng hợp.
2. **Một số tài liệu Markdown cũ còn quy tắc đã bị thay thế.** `SHIFT-DAILY-RULE.md` vẫn nói bỏ giới hạn lặp ca trong ngày; `POS-WEB-PAYMENT-DELIVERY.md` vẫn nói chưa mở giao hàng/bàn. Khi cập nhật đặc tả dùng quy tắc mới và code đã kiểm tra, không chép nguyên các đoạn cũ này.
3. **Chưa đồng bộ GitHub.** Các thay đổi working tree chưa commit/push; nhánh Web còn commit riêng. Không dùng nhãn “đã gộp mọi nhánh”.
4. **QR là mô phỏng có xác nhận.** Yêu cầu “quét là thành công” không đồng nghĩa thực tế chỉ GET trang QR được ghi tiền. Code hiện cố ý tách mở và xác nhận mô phỏng; tiền thật cần tích hợp nhà cung cấp.
5. **Tại bàn/giao hàng hiện có chế độ mô phỏng Development.** Không khẳng định đã có bản đồ, tính khoảng cách thật, xác thực QR bàn thật hoặc giao hàng Production.
6. **Giao diện chi tiết chưa hoàn toàn giống nhau.** Desktop đơn có 4 phần cơ bản. Trang chi tiết Web vẫn có hóa đơn và dữ liệu đối soát; dòng mô tả món tại Web còn in cả đá với món nóng. Không đánh dấu đã đơn giản hóa toàn bộ hai nền tảng giống nhau.
7. **Địa chỉ hóa đơn:** form Desktop hiện thêm địa chỉ giao khi đơn DELIVERY; không suy rằng mọi hóa đơn mang đi/tại bàn đã có địa chỉ hồ sơ khách như chi tiết đơn.
8. **Hủy một phần và nhận hoàn:** luồng Staff gửi hủy hỗ trợ phần chưa pha; màn hình chi tại quầy đang ưu tiên đơn CANCELLED. Cần kiểm riêng trường hợp chỉ hủy một phần để bảo đảm khoản được duyệt có đường nhận hoàn phù hợp; không coi mọi biến thể của đặc tả đã hoàn thiện.
9. **Nháp và thiết bị:** giỏ POS lưu theo phiên trong bộ nhớ, không khẳng định còn nguyên sau tắt ứng dụng. Mã máy dùng truy vết, không phải đăng nhập; còn mã tương thích thiết bị cũ không đồng nghĩa tái áp dụng quy trình cấp máy cho người dùng.

## 6. Vị trí code và bằng chứng

| Phần | File chính |
|---|---|
| Tài khoản/hồ sơ khách | `Features/Customer/Account`; `FourChang.Web/Pages/Account` |
| Giá menu mặc định | `Features/Customer/Menu/CatalogEndpoints.cs`; `wwwroot/js/catalog-updates.js` |
| Chọn món và giỏ Web | `wwwroot/js/cart-editor.js`, `cart.js`, `cart-page.js`, `cart-draft.js` |
| Checkout và phiên mới | `Pages/Checkout/Index.cshtml.cs`; `wwwroot/js/checkout-order.js`; `wwwroot/js/checkout-session.js` |
| Nhận món mô phỏng | `Features/Customer/Orders/SimulatedFulfillment.cs`; `CustomerOrdersEndpoints.cs` |
| QR mô phỏng | `Features/Payments/Simulation/PaymentSimulationService.cs`, `PaymentSimulationEndpoints.cs` |
| Đơn/hủy khách | `CustomerOrderReader.cs`, `CustomerOrderCancellation.cs`; `Pages/Orders` |
| Mở ca | `Features/Staff/WorkSessions/WorkEndpoints.cs`; `Desktop/Shell/EntryForm.cs` |
| Bán hàng POS | `Desktop/Staff/Sales/PosSalesPage.cs`, `PosItemEditorForm.cs`, `PosDraftCart.cs`; `Features/Staff/Sales/PosCheckoutService.cs` |
| Xử lý đơn | `Shared/Orders/OrderWorkflowService.cs`; `Desktop/Shared/Orders/OrdersWorkspacePage.cs`, `OrderRefresh.cs` |
| Duyệt hủy/hoàn tại quầy | `Shared/Orders/CancellationWorkflow.cs`; `Desktop/Shared/Orders/CancellationActions.cs` |
| Kết ca và số liệu | `Desktop/Staff/WorkSessions/ShiftPage.cs`; `Features/Admin/WorkSessions/WorkSessionService.cs` là service dùng để đọc số liệu, không bổ sung UC quản trị ở báo cáo |
| Hóa đơn | `Desktop/Staff/Invoices/StaffInvoicesPage.cs`; `Desktop/Shared/Invoices/InvoiceDetailsForm.cs` |

Các vị trí Backend bắt đầu từ `src/Backend/FourChang.Api`, Desktop từ `src/Presentation/FourChang.Desktop`, Web từ `src/Presentation/FourChang.Web`; DTO/client ở `src/Shared/FourChang.Presentation.Shared`.

Kết quả test đã ghi nhận trong các lượt triển khai trước: `daily-distinct-shifts.trx`, `daily-shift-picker-ui.trx`, `listed-variant-prices.trx`, `listed-price-recheck.trx`, `listed-default-browser.trx`, `staff-cancel-pending-summary.trx`, `staff-cancel-summary-recheck.trx`, `completed-shift-total.trx`, `staff-invoice-search.trx`. Một số lượt đầu có lỗi đã được sửa và kiểm lại; không trình bày mọi file TRX đều xanh hoặc coi chúng là một lần chạy toàn solution mới.

## 7. Nguồn đối chiếu

- Đặc tả gốc: `D:/8.tailieu/dac-25-Uc.docx` — ngày sửa file 06/11/2026; 8 UC-WEB, 5 UC-POS và BR-01 đến BR-36.
- Quyết định nghiệp vụ mới nhất trong cuộc trao đổi, `AGENTS.md`, code working tree ngày 13/11/2026.
- [GitHub main](https://github.com/hphuc071/DoAn_CSharp/tree/7431df11bc9dac42ec07fcbc78309e79359c467a).
- [Đối chiếu nhánh Web riêng](https://github.com/hphuc071/DoAn_CSharp/compare/7431df11bc9dac42ec07fcbc78309e79359c467a...3623f388bc8f3de1400be6054129d104721fe441).
- Tài liệu triển khai `STAFF-ORDER-WORKFLOW.md`, `ORDER-DETAILS-BASIC.md`, `INVOICE-BASIC-VIEW.md`, `POS-PREPARATION-REFINEMENT.md`, `SHORT-ORDER-CODES.md`, `POS-WEB-PAYMENT-FLOW.md`. Các tài liệu cũ được dùng để đối chiếu, không mặc định là quy tắc mới nhất.
