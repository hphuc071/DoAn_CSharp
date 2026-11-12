# Website khách hàng — phong cách cửa hàng cà phê

Tham khảo cách tổ chức của https://www.highlandscoffee.com.vn/ và https://order.highlandscoffee.com.vn/; giữ thương hiệu 4CHANG LINH NGU LAM, logo và hình sản phẩm hiện có. Không dùng logo, hình ảnh hoặc dữ liệu sản phẩm Highlands.

## Trình bày
- Header nâu #75452d, nền kem #f7f3ec, thẻ #fffdf9, chữ #30271f. Footer espresso hiện có.
- Banner rộng, tiêu đề lớn, CTA tới thực đơn; danh mục và bộ lọc giữ ID cũ.
- Menu 3 cột trên máy tính, 2 cột trên điện thoại. Thẻ ảnh lớn, tên/giá rõ và nút Chọn món.
- Cấu hình dùng dialog native, hỗ trợ Tab/Escape, nút đóng, lưu giỏ thành công thì đóng form; giỏ cũ giữ nguyên. Thông báo thêm món tự ẩn sau 6 giây.
- Hover/focus và hiệu ứng mở 180ms, tắt chuyển động theo prefers-reduced-motion.
- Header/footer và nút dùng chung trên các trang tài khoản/giỏ/thanh toán/đơn.

## File
- Pages/Shared/_Layout.cshtml: nhận diện và stylesheet chung.
- Pages/Menu/Index.cshtml: trang thực đơn và hộp cấu hình.
- wwwroot/css/storefront.css: toàn bộ lớp trình bày mới, tách khỏi nghiệp vụ.
- wwwroot/js/cart.js: mở/đóng dialog và phản hồi khi thêm món.
- wwwroot/js/cart-editor.js: chỉnh câu hướng dẫn cho khách, không thay xử lý giỏ/giá.
- tests/FourChang.Tests/KhachHang/StorefrontBrowserTests.cs: menu, lọc, giỏ, dialog, 1440/768/390px.

API, SQL, quyền, kiểm kho, định giá, thanh toán và đồng bộ thực đơn không thay đổi.

## Kiểm tra
Build: 0 warning, 0 error. Hai test Browser đạt: CustomerStorefront và CommercePresentationBrowserTests (customer-storefront-final.trx). Đã xem ảnh menu/dialog trên máy tính và điện thoại. Không khẳng định đã chạy toàn bộ suite.


## Đồng bộ luồng đặt món
- ordering.css áp dụng cùng nhận diện cho Cart, Checkout, Payment, Orders/Details; bảng tiền/thao tác giữ dữ liệu cũ.
- _CheckoutSteps.cshtml dùng chung ba bước điều hướng, không gửi lệnh thanh toán.
- Giỏ có nhóm số lượng và hộp sửa món native; đóng/Escape không lưu, chỉ Lưu mới cập nhật giỏ như trước.
- Thanh toán hiển thị rõ số dư cần trả, QR và thông báo thành công; nhãn mô phỏng luôn giữ.
- Hóa đơn trình bày dạng biên nhận, PDF vẫn tải theo handler và tài liệu đã lưu.
- Test CommercePresentationBrowserTests bổ sung QR chờ, bước hiện tại và sửa giỏ; không mở giao hàng/tại bàn hoặc thay quy tắc tiền/số điện thoại.
- Header đọc cùng giỏ cục bộ ở mọi trang, cập nhật theo thay đổi giỏ/tab khác; không ghi dữ liệu giỏ. Test kiểm tra cùng số ly xuyên suốt các trang.
- Câu hướng dẫn báo giá dùng từ ngữ khách hàng, bỏ chi tiết Backend khỏi nội dung hiển thị.

Kiểm tra luồng đặt món: build 0 warning/0 error, 2 test Browser đạt (customer-ordering-final.trx). Đã xem giỏ, nhận món, QR, hóa đơn và hộp sửa giỏ trên Desktop/mobile; chưa chạy toàn bộ suite hoặc ngân hàng thật.


## Chọn cấu hình và Thanh toán
- Size, Nóng/Lạnh, % đường và % đá là các trường riêng. Món nóng ẩn đá và chỉ nhận cặp định mức đá 0; món lạnh lọc đá theo mức đường đã chọn.
- Mọi lựa chọn vẫn ánh xạ về mã cấu hình và giá từ API, không khai báo giá riêng.
- Hộp chọn/sửa món có Thêm vào giỏ (hoặc Lưu món) và Thanh toán. Thanh toán dùng cùng kiểm tra giá mới nhất và lưu giỏ thành công rồi mới điều hướng /Checkout; không tự tạo đơn hoặc xác nhận chuyển tiền.
- Nút Thanh toán trong giỏ đi /Checkout; vẫn cần thông tin nhận món, xác nhận đơn và thanh toán QR theo luồng hiện có.
- Các test cũ chọn mã cấu hình dùng CartEditorActions để thao tác trên các trường hiển thị mới; kiểm tra mã/cặp giả mạo vẫn giữ nguyên.

Kiểm tra: build 0 warning/0 error. Bốn test menu/chọn tiếp/nóng-lạnh/thanh toán/commerce đạt trong split-product-options.trx; sau cập nhật các kỳ vọng UI cũ, 16 test CartDraftBrowserTests đạt trong split-cart-regression.trx. Đã xem ảnh nóng/lạnh thực tế. API và SQL không đổi.


## Tự bắt đầu phiên tiếp theo sau thanh toán
- checkout-session.js chỉ đọc trạng thái đơn thuộc khách hiện tại qua handler State; chỉ dọn intent khi paid=true và orderId khớp.
- State giữ trường status cũ, bổ sung orderId/paid dựa trên thu tiền VERIFIED và số còn phải trả, không tự xác nhận tiền.
- Dùng cùng Web Lock với lệnh tạo đơn, so sánh nguyên bản lưu trước khi bỏ. Không xóa giỏ món mới; không xóa thao tác thiếu biên nhận hoặc mất kết nối.
- Bỏ nút Bắt đầu đơn tiếp theo; Checkout tự cập nhật khi phiên đã thanh toán kết thúc. Thanh toán chưa đủ/chưa xác minh vẫn giữ mã chống trùng và biên nhận.

Kiểm tra tự kết thúc phiên: build 0 warning/0 error. 40 kiểm tra đạt lượt đầu, một kỳ vọng câu thông báo cũ đã cập nhật. Lượt cuối 7 test đạt, gồm form tự mở lại, giữ giỏ mới, chưa trả tiền, sai đơn, 401/503. Test Commerce với API/SQL fixture đã xác minh xóa phiên sau thu tiền thành công (automatic-checkout-session.trx và automatic-checkout-session-final.trx).

