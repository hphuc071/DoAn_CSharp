# Đơn hàng, duyệt hủy và hoàn tiền tại quầy

## Luồng đã chốt

1. Khách chỉ gửi yêu cầu khi đơn Web Chờ xác nhận. Lưu nhóm HuyMon REQUESTED, chưa hủy dòng món, chưa giảm tiền/giữ chỗ, chưa tạo hồ sơ hoàn.
2. Nhân viên chọn đơn, bấm **Chuyển yêu cầu hủy**. Chỉ ghi chuyển; không có quyền duyệt.
3. Quản lý chọn đơn đã chuyển, bấm **Duyệt hủy / hoàn tại quầy** và xác nhận người nhận, hoặc **Thao tác khác → Từ chối yêu cầu hủy** có lý do.
4. Khi duyệt: cùng transaction hủy toàn bộ dòng chưa pha, giải phóng giữ chỗ, đổi đơn CANCELLED, tạo hồ sơ CASH APPROVED theo khoản thu đã xác minh. Chưa có khoản chi thành công ở bước này. Khi từ chối: giữ tiền/kho và cho tiếp nhận bình thường; khách thấy lý do.
5. Khách đến quầy cung cấp mã đơn. Nhân viên mở **Hóa đơn → Hoàn tiền tại quầy**, chọn đơn, bấm **Hoàn tiền tại quầy**, đối chiếu họ tên, ghi biên nhận, xác nhận đã giao tiền và khách đã nhận.
6. Máy chủ kiểm tra hồ sơ được duyệt, phiên ca OPEN, người nhận khớp, hạn mức và quỹ ca còn đủ (trừ khoản chi chờ đối soát). Ghi LanChiHoanTien SUCCEEDED gắn ca; hồ sơ REFUNDED. Không sửa khoản thu chuyển khoản gốc. Đối chiếu ca trừ khoản tiền mặt đã hoàn theo công thức hiện có.

## Quy tắc bảo toàn

- Trong thời gian chờ xử lý yêu cầu, chặn tiếp nhận/pha/hủy trực tiếp để tránh vừa pha vừa duyệt hủy. Không đổi mã trạng thái đơn để biểu diễn trạng thái yêu cầu.
- Duyệt/từ chối bắt buộc sau bước Staff chuyển. Staff không gọi API Admin hoặc tự duyệt tiền.
- Mỗi lệnh có GUID chống trùng và rowversion; timeout thử lại đúng nội dung/mã. Hai lần bấm không chi hai lần. Lỗi audit rollback cả tiền/kho/trạng thái.
- Mỗi hồ sơ khoản thu có biên nhận riêng. Nếu đơn có nhiều hồ sơ được duyệt, xử lý lần lượt đến khi hết khoản còn chi.
- Đơn/hoàn cũ không tự chuyển thành quy trình mới. Hướng dẫn đến quầy chỉ hiện với hồ sơ CASH được duyệt; hồ sơ ngân hàng cũ giữ nguyên.
- Không thêm/bớt bảng, cột, FK; tái sử dụng HuyMon, HoSoHoanTien, LanChiHoanTien, NhatKyHeThong và SuKienDongBo. Hoàn không tự sửa hóa đơn gốc; các thao tác hóa đơn điều chỉnh hiện có được giữ nguyên.

## Tích hợp nhóm

- Workspace dùng chung: src/Presentation/FourChang.Desktop/Shared/Orders. Wrapper Admin/Staff nằm trong thư mục actor tương ứng.
- Service: src/Backend/FourChang.Api/Shared/Orders/CancellationWorkflow.cs. DTO: src/Shared/FourChang.Presentation.Shared/Contracts/Orders.
- Khách: POST /api/customer/orders/{id}/cancel (Reason, ExpectedVersion, header Idempotency-Key), giữ URL cũ; kết quả giờ là REQUESTED, không còn xác nhận hủy ngay. Website gửi qua Razor POST có antiforgery.
- Staff/Admin: POST /api/pos/orders/{id}/cancellation hoặc /api/admin/orders/{id}/cancellation. Lệnh FORWARD/APPROVE/REJECT dùng đúng vai trò.
- Staff: GET /api/pos/orders/{id}/counter-refunds; POST /api/pos/orders/counter-refunds/{id}; GET /api/pos/orders/counter-refunds/operations/{guid}.
- Snapshot thêm YeuCauHuy; snapshot khách thêm KetQuaYeuCauHuy, phương thức hồ sơ hoàn. Không khai báo giá hoặc tự tính hạn mức ở giao diện.
- Local .env đã bật Orders__SqlIntegrationValidated=true và Orders__CancellationEnabled=true sau kiểm thử SQL. Các máy thành viên sao chép .env.example rồi bật hai cờ này với database đã chuẩn bị; không đưa .env thật lên Git. Không tự bật các worker/xác minh tiền ngân hàng khác.

## Kiểm thử

CancellationApprovalTests: phân quyền, chờ chuyển/duyệt, không hủy sớm, từ chối, giải phóng giữ chỗ, đủ quỹ, người nhận, chi đồng thời/gửi lại, rollback lỗi audit, chốt ca sau hoàn.
CancellationRequestBrowserTests: gửi form thật có CSRF/version, trạng thái chờ và responsive 390px.
CancellationDesktopTests: worker WinForms bấm dialog thật qua ba bước Staff chuyển → Admin duyệt → Staff chi.
Kiểm tra hồi quy OrderReadTests, OrderRazorOfflineTests, StaffOrderWorkflowTests và thanh công cụ đơn Admin. Kết quả nằm ở artifacts/test-results/cancellation-final.trx.

## Tách danh sách và cập nhật nền

- Nhân viên: menu Đơn hàng cho danh sách xử lý; menu Đơn hủy cho yêu cầu đang chờ chuyển/duyệt và đơn đã hủy.
- Quản lý: menu Đơn hàng và Đơn hàng hủy riêng nhau, cùng phạm vi UC-ADM-07; không thêm mã UC.
- API danh sách hỗ trợ NhomDon=NORMAL hoặc CANCELLATION. Bỏ tham số vẫn trả danh sách tổng hợp như hợp đồng cũ. Lọc nhóm trước Count/Skip/Take; yêu cầu bị từ chối trở lại nhóm NORMAL. EXPIRED vẫn tách nghĩa với CANCELLED.
- Polling 3 giây dùng bộ lọc đã áp dụng, không lấy ô đang nhập dở, không bật loading overlay. Không polling khi dialog/menu/lệnh đang mở. Snapshot không đổi giữ nguyên DataSource; có thay đổi giữ mã đơn đang chọn và vị trí cuộn/cột. Timer được dừng khi rời màn hình.
- Kiểm thử: OrderListRefreshTests (phân nhóm/phân trang, trở lại sau từ chối, polling qua hai chu kỳ không đổi binding/loading, dữ liệu đổi vẫn cập nhật), CancellationDesktopTests đi qua menu hủy mới; hồi quy StaffOrderWorkflowTests, EntryUiTests, CompactOrderToolbar. 10 test đạt tại artifacts/test-results/split-orders-stable-refresh.trx; build không warning/error.
## Tinh chỉnh giao diện theo luồng gửi duyệt

- Trang Đơn hủy của Nhân viên chỉ có một nút nghiệp vụ Gửi yêu cầu duyệt. Sau khi gửi, nút khóa để chống gửi lại; bảng hiện Chờ Quản lý duyệt. Quản lý duyệt xong hiện Hủy thành công màu xanh. Đây là thành công của hủy, chưa phải đã chi tiền.
- Chi tiền được đặt riêng tại Hóa đơn → Hoàn tiền tại quầy, giữ nguyên xác minh người nhận, kiểm tra quỹ và audit. Không xóa chức năng hoàn tiền.
- Bộ lọc đơn chia hai hàng, giữ đủ nguồn/hình thức/trạng thái/ngày/ca; bảng bớt cột kỹ thuật, có badge và thời điểm dễ đọc. Giữ màu và nhận diện hiện có; polling không dựng lại bảng.
- API list nhóm thêm COUNTER_REFUND để phân trang đúng các hồ sơ tiền mặt được duyệt/đã chi, không trộn yêu cầu chưa được duyệt.
- Snapshot danh sách khách thêm CanReceiveCounterRefund (mặc định false cho client cũ). Chỉ bật khi đơn của chính khách đã hủy và có khoản CASH được duyệt còn chưa chi. Thẻ đơn hiển thị: Vui lòng đến quầy để nhận hoàn tiền. Hoàn xong thông báo biến mất; không hiện chỉ vì đơn có trạng thái CANCELLED.
- Kiểm thử giao diện thật: chỉ một nút gửi, trạng thái thành công sau duyệt, chi qua tab riêng, thông báo trên danh sách khách trước/sau chi và responsive 390px; hồi quy phân nhóm, tiền/kho, dữ liệu riêng khách và polling. 56 test đạt tại artifacts/test-results/order-cancellation-refinement-final.trx. Kiểm riêng focus ô đang nhập tại order-refresh-input-focus.trx.