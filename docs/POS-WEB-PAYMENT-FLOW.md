# Bán hàng POS, cấu hình Web và thanh toán mô phỏng

## Phạm vi đã được xác nhận

- Nhân viên gửi đối chiếu thành công quay về màn hình mở ca. Đây không phải chốt ca: ca nguồn vẫn LOCKED chờ Admin. Chốt trực tiếp thành công cũng quay về mở ca. Mở ca lại vẫn phải đối chiếu hồ sơ/trạng thái trên server.
- POS có giỏ nhiều dòng; mỗi dòng giữ cấu hình, số ly, topping và ghi chú riêng. Tên giống nhau không làm gộp nhầm biến thể. Có sửa/bỏ dòng; giá và cấu hình đọc từ menu chung.
- Web chọn món chuyển vùng nhìn/focus tới cấu hình. Sau thêm thành công chỉ xóa form cấu hình cũ, giữ toàn bộ món đã có trong giỏ và đưa khách về thực đơn để chọn tiếp. Lỗi lưu không dọn dữ liệu nhập.
- Theo yêu cầu mới: QR thanh toán **mô phỏng**, có nhãn riêng, không chuyển tiền thật. Quét/mở QR chỉ đọc; bấm xác nhận thử nghiệm mới cập nhật trạng thái. Web/POS đọc lại server để tự cập nhật.

## Cách chạy và kiểm thử thủ công

1. Mở Docker Desktop; SQL phải healthy. Chạy `Chay-Backend.cmd`.
2. Đóng cửa sổ POS cũ rồi chạy `Chay-NhanVien.cmd` để build/nạp DLL mới.
3. Nhập mã/họ tên nhân viên, chọn ca và mở ca. Trên Bán hàng bấm **+** của món, chọn size/nhiệt độ, cặp đường/đá, topping, số ly, ghi chú rồi thêm. Thêm món thứ hai; kiểm tra giỏ vẫn có cả hai.
4. Chọn Mang đi hoặc Tại bàn (bắt buộc chọn bàn đang hoạt động). Với tiền mặt, nhập đủ tiền khách đưa rồi thu. Server tạo đơn, khoản thu và hóa đơn nguyên tử; giỏ chỉ dọn khi nhận kết quả đã xác nhận. Tiền thừa tính từ tổng đã được server chấp thuận.
5. Với chuyển khoản, tạo đúng một đơn chờ; mở QR mô phỏng. Có thể bấm **Mở trang xác nhận mô phỏng** trên cùng máy, xác nhận ở trang mới rồi quay lại. Không bấm thu lại khi mất mạng/đóng QR; dùng nút mở QR/kiểm tra của đơn cũ.
6. Website chạy bằng `Chay-Web.cmd`. Đăng nhập khách, chọn món → cấu hình → thêm giỏ → Checkout → xác nhận báo giá/đặt mang đi → **Thanh toán đơn này**. Trang QR tự tải trạng thái sau khi xác nhận mô phỏng.
7. Nhân viên nhập thực đếm/lý do tại Ca làm việc và Gửi đối chiếu: quay về mở ca; Admin vào Ca làm việc đối chiếu/chốt. Không tự đổi LOCKED thành CLOSED chỉ vì trở về màn hình đầu.

Web hiện vẫn giữ phạm vi đặt mang đi đã có; không mở thêm tạo đơn giao hàng/QR bàn chưa được xác minh. POS hỗ trợ mang đi và tại bàn, không có phí giao hàng POS.

## Cấu hình local

`.env` local đã bật rõ các cờ sau; không commit file này:

```dotenv
ASPNETCORE_ENVIRONMENT=Development
Payments__SimulationEnabled=true
Payments__SimulationPublicBaseUrl=http://localhost:15881
Orders__CustomerCreationEnabled=true
Checkout__OrderCreationEnabled=true
Orders__PendingPaymentTimeoutMinutes=15
```

15 phút là giá trị cấu hình local cho trình diễn, không phải hằng số trong service. POS chuyển khoản từ chối tạo nếu thiếu thời hạn hợp lệ. `.env.example` giữ các cờ ghi/mô phỏng mặc định false để thành viên chủ động bật tại máy mình. Script Web chỉ đọc các cờ giao diện cần thiết, không chuyển mật khẩu SQL sang Web.

QR mặc định chứa URL localhost, dùng trên cùng máy. Nếu quét bằng điện thoại, localhost là chính điện thoại: phải cấu hình `Payments__SimulationPublicBaseUrl` thành địa chỉ IP LAN của máy chạy API, cho API lắng nghe địa chỉ LAN tương ứng và cho phép kết nối trong mạng tin cậy. Không tự mở firewall hoặc đưa server ra Internet trong đợt này. Link mở trên cùng máy luôn là cách thử không cần cấu hình mạng LAN.

## Trạng thái và nguồn dữ liệu

| Tình huống | Kết quả |
|---|---|
| POS thu đủ CASH | ThanhToan VERIFIED, DonHang ACCEPTED, một hóa đơn gốc, BOM giữ chỗ |
| POS BANK_TRANSFER mới tạo | ThanhToan PENDING, đơn PENDING_PAYMENT; chưa xác nhận thu |
| POS xác nhận QR mô phỏng | VERIFIED, đơn ACCEPTED; không tự pha hoặc hoàn tất |
| Web xác nhận QR mô phỏng | VERIFIED, đơn PENDING_CONFIRMATION; nhân viên tiếp nhận theo luồng hiện có |
| Chỉ lấy ảnh QR/quét mở trang | Không ghi tiền hoặc đổi trạng thái |
| Xác nhận lặp/đồng thời | Một giao dịch SIMULATION, một hóa đơn; không thu hai lần |
| Hết hạn/giá thay đổi/thiếu tồn | Chặn ghi, giữ dữ liệu để xử lý |

Không thêm hoặc xóa bảng/cột. POS dùng `OrderPricingService`, thuật toán `OrderBomAllocation` và các bảng đơn/dòng/topping/BOM/giữ chỗ/thu/hóa đơn/audit/outbox hiện có. Tồn vật lý chưa trừ khi thu: vẫn trừ theo luồng bắt đầu pha đã có. Giao dịch mô phỏng lưu nhà cung cấp `SIMULATION`, tài khoản `DEMO-NO-REAL-ACCOUNT`, audit `SIMULATED_PAYMENT_CONFIRMED` để phân biệt rõ. Vì dữ liệu mô phỏng có ảnh hưởng báo cáo, chỉ dùng database đồ án/thử nghiệm, không dùng database vận hành thật.

## Thay bằng thanh toán ngân hàng sau này

Không chỉ thay ảnh QR: phải kiểm tra đúng ngân hàng, tài khoản nhận, số tiền, nội dung và hạn, đồng thời xác thực callback/sao kê từ nhà cung cấp trước ghi nhận tiền.

- Giữ `IPaymentQrSource` để cấp QR thật qua adapter tin cậy.
- Dùng `IBankCallbackProtocol`/`IPaymentVerificationSource` và `SqlBankPaymentService` hiện có cho chứng cứ giao dịch thật; triển khai theo tài liệu nhà cung cấp đã chọn.
- Tắt `Payments__SimulationEnabled`, dùng môi trường Production và cấu hình callback/bí mật ngoài Git. Production chặn endpoint mô phỏng ngay cả khi ai bật nhầm cờ.
- Không đưa nút “Tôi đã chuyển khoản” thành xác minh thu thật; không dùng token mô phỏng để gọi luồng xác thực ngân hàng.

## File và điểm tích hợp

| Nhóm | Vị trí |
|---|---|
| POS API | src/Backend/FourChang.Api/Features/Staff/Sales/PosCheckoutService.cs; PosCheckoutEndpoints.cs |
| Mô phỏng | src/Backend/FourChang.Api/Features/Payments/Simulation/PaymentSimulationService.cs; PaymentSimulationEndpoints.cs |
| DTO/client | src/Shared/FourChang.Presentation.Shared/Contracts/Staff/PosCheckoutContracts.cs; ApiClients/Staff/PosCheckoutClient.cs |
| POS UI | src/Presentation/FourChang.Desktop/Staff/Sales/PosSalesPage.cs; PosItemEditorForm.cs; PosDraftCart.cs; PosPaymentDialog.cs; SalePage.cs |
| Ca làm | src/Presentation/FourChang.Desktop/Staff/WorkSessions/ShiftPage.cs |
| Web | wwwroot/js/cart.js; cart-editor.js; checkout-order.js; payment-simulation.js; Pages/Checkout/Index.cshtml; Payment.cshtml; Payment.cshtml.cs |
| Khởi chạy | scripts/Start-LocalWeb.ps1; .env.example; .env local ngoài Git |
| Thư viện | QRCoder 1.6.0 phía API, tạo ảnh cục bộ, không gửi nội dung đơn sang dịch vụ tạo QR ngoài |

Khi gộp nhánh phải lấy cả Backend, Shared contract/client, Desktop, Web và tests; giữ các đăng ký mới trong Program.cs. Không chỉ chép form hoặc bỏ phần kiểm tra quyền/giá/BOM.

## Chạy test

```powershell
dotnet build tests/FourChang.Tests/FourChang.Tests.csproj
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "FullyQualifiedName~PosCheckoutTests|FullyQualifiedName~PosSalesUiTests|FullyQualifiedName~ChooseNextProductBrowserTests|FullyQualifiedName~SimulationPaymentBrowserTests|FullyQualifiedName~SimulationSafetyTests|UseCase=UC-ADM-12"
```

Cùng một csproj xUnit, test tách theo actor/UC; SQL sử dụng database GUID riêng. QR trong test tự trỏ API fixture, không gọi localhost của database quán.
