# Hóa đơn cơ bản — 13/11/2026

Cửa sổ xem hóa đơn của Nhân viên và Quản lý dùng chung InvoiceDetailsForm với 5 phần:
- Thông tin quán: logo/tên thương hiệu hiện có; không tự đặt địa chỉ hay điện thoại quán khi chưa có dữ liệu.
- Thông tin hóa đơn: số hóa đơn, mã đơn WEB/POS ngắn, ngày phát hành, trạng thái và loại chứng từ.
- Khách hàng: ưu tiên tên người mua trên hóa đơn, sau đó người nhận của đơn; thiếu tên dùng Khách lẻ. Điện thoại và địa chỉ lấy từ thông tin nhận món của đơn; địa chỉ chỉ hiện với giao hàng.
- Danh sách món: đọc tên/size/số lượng/giá và topping từ snapshot hóa đơn. Đơn giá gồm topping mỗi ly; số ly là số đã phát hành, không thay bằng số còn lại sau hủy.
- Thanh toán: tiền món, phí giao khi có, tổng giá trị chứng từ, phương thức và tình trạng thu/hoàn thực tế của đơn.

Không hiện JSON, mã nội bộ, mã hoàn/hủy hay tab nhật ký trong hộp thoại cơ bản. Không thay số hóa đơn, dữ liệu gốc, database, API hoặc logic thanh toán. Chức năng in/PDF/điều chỉnh của Quản lý giữ nguyên.

Hóa đơn cũ/điều chỉnh không có mảng món trong snapshot sẽ hiện giải thích thay vì lấy giá menu mới hoặc tạo dòng giả. Điện thoại/địa chỉ của đơn liên quan là dữ liệu nhận món đang lưu, không khẳng định có trong snapshot hóa đơn cũ.

Các file chính:
- src/Presentation/FourChang.Desktop/Shared/Invoices/InvoiceDetailsForm.cs
- src/Presentation/FourChang.Desktop/Shared/Orders/OrderDetailsForm.cs (chia sẻ helper bố cục)
- src/Presentation/FourChang.Desktop/Staff/Shared/StaffDocumentsPage.cs
- src/Presentation/FourChang.Desktop/Admin/Invoices/InvoicesManagementPage.cs
- tests/FourChang.Tests/NhanVien/InvoiceSnapshotTests.cs
- tests/FourChang.Tests/QuanLy/DungChung/StaffDocumentsUiChecks.cs

Kiểm tra: build, 8 test snapshot/API/WinForms trong basic-invoice-details.trx; kiểm tra thêm bố cục tên/điện thoại/địa chỉ ở basic-invoice-customer-ui.trx. Ảnh trong artifacts/tests/xunit/ADM01-05/<database>/staff-invoice-*.png. Biến thể giao hàng trong test UI chỉ kiểm hiển thị, không thay đổi đơn thật.
