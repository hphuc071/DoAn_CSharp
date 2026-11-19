# UC-ADM-06 — tái sử dụng thư mục UC-ADM-08/09
- Tìm hóa đơn theo mã đơn/khách và khoảng ngày; hóa đơn gốc/snapshot không đổi khi xem, in hoặc tải PDF.
- Duyệt hủy tạo khoản đã duyệt; trạng thái Chờ khách nhận, đã trả=0 và còn phải trả đúng phần hủy đã thu tiền.
- Nhân viên trả tiền thật và replay chỉ có một lần chi; Quản lý thấy đúng người chi/ca/thời gian và số đã trả.
- API quản lý cũ chặn sửa/duyệt/chi/lập bản trùng khoản quầy; không đổi version hoặc sinh thêm lần chi.
- Bộ lọc ngày/kết quả hợp lệ; Customer không truy cập danh sách quản lý. Pending/failed/reconciling không tính đã trả.
- UI hai trang: bộ lọc và bảng gọn; hóa đơn 3 nút, hoàn tiền 1 nút xem; không có ô nhập tiền hay duyệt lại, không JSON kỹ thuật. Polling giữ DataSource, resize/chi tiết không tràn.
- Giữ hồi quy chứng từ điều chỉnh có nguồn và hồ sơ ngân hàng cũ, không mở lại thao tác hủy/chốt hết hạn đã bỏ.