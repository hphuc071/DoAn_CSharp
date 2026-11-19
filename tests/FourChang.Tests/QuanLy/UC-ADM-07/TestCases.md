# UC-ADM-05 — tái sử dụng thư mục UC-ADM-07
- Admin không được gọi ACCEPT/START/FINISH/DELIVER/COMPLETE/CANCEL/RETRY_DELIVERY/EXPIRE; đơn/kho/version không đổi.
- Nhân viên gửi yêu cầu đúng món/số ly; chưa duyệt chưa hủy.
- Duyệt hoặc từ chối có phiên bản/mã chống trùng; không chi tiền sớm, không tạo hồ sơ hoàn cho đơn chưa thu tiền.
- Giữ kết quả đã từ chối/đã duyệt, lọc tại máy chủ trước phân trang; đọc địa chỉ snapshot.
- Giao thất bại chỉ cần lý do, không hồi kho, replay không lặp.
- WinForms: Admin chỉ xem/ngoại lệ được phép; nút duyệt/từ chối và trạng thái rõ, resize không cắt bộ lọc.
- Hồi quy chuỗi Khách → Nhân viên → Quản lý → Nhân viên hoàn tại quầy và tiến trình pha chế POS.
