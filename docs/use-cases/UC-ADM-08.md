# UC-ADM-08 — Quản lý nhân viên và ca làm việc

## Nhân viên
- Tìm tên/điện thoại, lọc Đang làm/Ngừng làm và phân trang; thêm/sửa họ tên, điện thoại, địa chỉ, ngày vào làm. Không tạo tài khoản/mật khẩu POS.
- Giao diện giữ4thao tác: Thêm / Sửa / Ngừng hoặc Kích hoạt / Lịch sử ca. Nhấp đúp để xem hồ sơ. Không xóa lịch sử trên giao diện; API xóa cũ chỉ tương thích hồ sơ hoàn toàn chưa tham chiếu.
- Ngừng làm phải có lý do, kiểm tra phiên bản và chặn nếu còn ca chưa kết thúc hoặc đang chờ nhận bàn giao. Kích hoạt không mở lại ca cũ. Đổi hồ sơ không sửa tên snapshot khi đã mở ca.

## Lịch sử và kết quả ca
- Tìm theo tên hoặc mã nhân viên, lọc ngày/tên ca/trạng thái, phân trang; lịch sử từ hồ sơ giữ đúng mã nhân viên để phân biệt trùng tên. Thông tin máy giữ lịch sử nội bộ, không bắt nhập để tra cứu/bàn giao.
- Chi tiết: thông tin ca; số đơn và tổng tiền đơn hoàn thành; tiền đầu ca/hệ thống/kiểm đếm/chênh lệch;3bảng Đơn hoàn thành / Đơn cần bàn giao / Thu-hoàn tiền.
- Tổng đơn chỉ gồm COMPLETED do ca đó hoàn thành, lấy giá/nghĩa vụ đã chốt. Nếu đơn nhận bàn giao rồi hoàn tất ở ca mới, chỉ tính vào ca hoàn tất. Dữ liệu cũ thiếu audit hoàn thành mới fallback ca gắn với đơn. Dùng chung predicate cho chi tiết ca và báo cáo UC-ADM-07 để không lệch nhau.
- Tiền mặt hệ thống = đầu ca + thu CASH đã xác minh của chính ca − hoàn CASH thành công của chính ca. Chuyển khoản không cộng vào két. NULL thực đếm hiện Chưa gửi, không giả0.

## Kết ca, bàn giao và phục hồi
1. Nhân viên kết ca khi không còn đơn/khoản chưa rõ và kiểm đếm khớp. Ca CLOSED chỉ đọc, không có bước Quản lý phê duyệt lại hay mở lại.
2. Còn việc dở: Nhân viên gửi đối chiếu; Quản lý chọn người nhận và xác nhận đúng toàn bộ danh sách đơn/khoản chưa rõ. Thiếu/thừa danh sách, nguồn thay đổi hoặc tiền chưa khớp phải báo lỗi, không bàn giao một phần.
3. Ca nguồn HANDED_OVER; người nhận tự mở ca tại POS. Liên kết đúng nhân viên nhận, không dùng mã máy xác thực/khóa máy dùng chung. Không chiếm người nhận đang có ca hoặc bàn giao chờ khác.
4. Đơn nhận từ ca trước được tách rõ; khoản thu/chi cũ giữ nguyên ca phát sinh, không đổi MaCaLamViec để ép khớp số tiền.
5. Mỗi nhân viên được làm Sáng/Chiều/Tối mỗi loại tối đa một lần trong ngày UTC+07; ngày mới được dùng lại, không buộc khung giờ thực tế. Receipt mở ca cũ không tạo thêm ca.
6. Mất phiên: Quản lý cấp mã phục hồi một lần, đúng nhân viên/ca,15phút; chỉ đổi sang phiên gửi kiểm đếm, không bán hàng. Ca phục hồi/legacy còn LOCKED chờ đối chiếu có thể chốt theo số nhân viên đã gửi khi đủ điều kiện. Đây là xử lý ca chưa kết thúc, không phải xác nhận lại ca đã CLOSED. Mã bí mật không lưu audit/receipt.
7. Giữ kiểm tra quyền, version, nguồn đối chiếu, audit/receipt và giao dịch nguyên tử; không có tạm khóa/đề nghị tiếp tục hay Admin nhập tiền cuối ca thay nhân viên.

## Triển khai
Tái sử dụng EmployeesPage/EmployeeEditorForm, WorkSessionsPage/WorkSessionDetailsForm, EmployeeService/WorkSessionService/ShiftRecoveryService/HandoverLink và test UC-ADM-11/12 cũ. Thêm DTO danh sách đơn hoàn thành/mã đơn giao dịch, lọc tên tại API; không thay schema hoặc chuyển đổi/xóa dữ liệu quán.