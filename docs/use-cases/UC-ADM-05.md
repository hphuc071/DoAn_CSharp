# UC-ADM-05 — Quản lý đơn hàng và yêu cầu hủy

## Phạm vi
Quản lý tìm theo mã WEB/POS hoặc tên khách; lọc ngày, nguồn, hình thức, trạng thái và ca; xem chi tiết bốn phần: đơn, khách, món, thanh toán. Không thay snapshot giá/công thức/địa chỉ.

## Yêu cầu hủy
1. Khách gửi đề nghị, Nhân viên chuyển; hoặc Nhân viên gửi đề nghị trực tiếp cho đúng món/số ly chưa pha.
2. Quản lý xem lý do và món/số ly yêu cầu, chọn Duyệt yêu cầu hủy hoặc Từ chối, nhập lý do xử lý.
3. Chỉ duyệt yêu cầu đã được chuyển. Duyệt mới hủy đúng phần chưa pha, giải phóng giữ chỗ tương ứng; không hoàn lại nguyên liệu đã tiêu hao.
4. Nếu có tiền cần hoàn, tạo khoản được duyệt để khách nhận tại quầy. Duyệt hủy chưa phải đã chi tiền. Nhân viên xác nhận giao tiền thật trong luồng hoàn tại quầy.
5. Từ chối giữ đơn/kho/tiền nguyên trạng. Danh sách riêng lưu kết quả chờ xử lý/đã duyệt/đã từ chối, lọc tại máy chủ trước phân trang. Một đơn có nhiều yêu cầu thì hiển thị yêu cầu đang chờ hoặc quyết định gần nhất; nhật ký vẫn giữ các quyết định cũ.

## Phân quyền và ngoại lệ
- Tiếp nhận, bắt đầu pha, pha xong, giao/hoàn tất thuộc Nhân viên. Admin không hủy trực tiếp, đổi địa chỉ sau đặt, giao lại hoặc chốt hết hạn bằng nút thủ công cũ.
- Đơn đang giao có thể ghi nhận Giao thất bại kèm lý do; không đánh dấu hoàn tất giả, không tự hoàn kho/chi tiền.
- API kiểm quyền độc lập giao diện, phiên bản, mã chống trùng, trạng thái và số ly. Kho/đơn/hồ sơ/audit cùng giao dịch; lỗi phải rollback, gửi lại không tạo kết quả lần hai.

## Triển khai
Tái sử dụng OrdersWorkspacePage, OrderAdministrationService, CancellationWorkflow và thư mục kiểm thử UC-ADM-07 cũ. Không đổi schema hoặc di chuyển dữ liệu quán.
