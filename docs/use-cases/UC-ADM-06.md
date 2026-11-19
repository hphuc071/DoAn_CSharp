# UC-ADM-06 — Quản lý hóa đơn và theo dõi hoàn tiền

## Hóa đơn
- Tìm theo số hóa đơn, mã đơn WEB/POS hoặc khách; lọc từ ngày–đến ngày và ca, phân trang tại máy chủ.
- Danh sách chỉ số hóa đơn, mã đơn, khách, số tiền và ngày phát hành. Không hiển thị loại hóa đơn trong danh sách/chi tiết; thuộc tính vẫn giữ nội bộ để bảo toàn lịch sử. Xem chi tiết gồm quán, hóa đơn, khách, món và thanh toán.
- Ba thao tác: Xem hóa đơn, In hóa đơn, Tải PDF. In lại có bản sao, lưu log gửi/thất bại; không phát hành hóa đơn mới hoặc thay số tiền khi xem/in/tải.
- Nội dung món và đơn giá lấy snapshot đã phát hành; thiếu dữ liệu hóa đơn cũ phải báo thiếu, không dựng từ menu mới. Không sửa/xóa bản gốc.

## Hoàn tiền tại quầy
1. UC-ADM-05 duyệt hủy đã xác định khoản cần hoàn từ tiền đã thu và phần hủy hợp lệ; không duyệt lần hai ở UC này.
2. Quản lý chỉ tra cứu/theo dõi. Tìm mã đơn/người nhận, lọc ngày phát sinh và kết quả: Chờ khách nhận / Đã hoàn tiền / Đã hoàn một phần / Cần kiểm tra.
3. Hiện mã đơn, người nhận, cần hoàn, đã trả, còn phải trả và ngày phát sinh. Xem chi tiết có lý do và lịch sử thời gian/nhân viên/ca/số tiền/kết quả.
4. Nhân viên kiểm tra người nhận và trả tiền tại quầy, xác nhận khách đã nhận. Chỉ SUCCEEDED cộng vào đã trả; pending, đối soát chưa rõ hoặc thất bại không tính thành công. Mã chống trùng ngăn chi lặp.
5. Hồ sơ cũ không đủ điều kiện/không phải hoàn tại quầy giữ để tra cứu, báo Cần kiểm tra khi chưa chứng minh trả đủ; không tự duyệt hoặc chuyển thành thành công.
6. API quản lý cũ không được sửa/duyệt/chi/xác minh lại khoản CASH/CANCELLATION thuộc luồng quầy, kể cả đổi sang ngân hàng hoặc lập bản trùng cùng nguồn. Không chỉ ẩn nút ở UI.

## Ràng buộc và phạm vi
- Không bổ sung quy trình ngân hàng, không nhập tiền hoàn tùy ý; giữ dịch vụ ngân hàng/lịch sử cũ tương thích ngoài giao diện tinh gọn.
- Không thay schema, không chuyển đổi hoặc xóa dữ liệu thật. Các snapshot, audit, hạn mức và giao dịch cũ được giữ nguyên.
- Polling nền giữ bộ lọc đã áp dụng/chọn dòng/chỗ cuộn, không bind lại khi dữ liệu không đổi. Khi lỗi mạng giữ dữ liệu đã tải, không giả số tiền bằng 0.
- Tái sử dụng InvoicesManagementPage, RefundsManagementPage, client/DTO/service và test thư mục UC-ADM-08/09 cũ.