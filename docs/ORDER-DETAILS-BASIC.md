# Chi tiết đơn hàng cơ bản

Cửa sổ Chi tiết dùng chung cho POS và Quản lý chỉ hiển thị:
1. Thông tin đơn: mã, thời gian đặt, trạng thái, Web/POS, hình thức nhận.
2. Thông tin khách: tên, điện thoại; bàn khi tại bàn. Đơn giao hàng dùng địa chỉ đã chốt; đơn khác hiển thị địa chỉ hồ sơ khách đang dùng, ưu tiên mặc định, thiếu thì Chưa cung cấp.
3. Danh sách món: tên, size, nóng/lạnh, đường, đá khi lạnh, topping/ghi chú nếu có, số ly còn lại, đơn giá gồm topping và thành tiền.
4. Thanh toán: tiền món, phí giao khi giao hàng, tổng tiền, phương thức, trạng thái theo số thực thu/hoàn.

Không hiển thị nguyên liệu, giữ chỗ, tiêu hao, lịch sử hoặc ID kỹ thuật trong cửa sổ này. Các dữ liệu vẫn được lưu và nghiệp vụ kho/tiền/phân quyền không thay đổi. Dòng có hủy ghi rõ số đã hủy, thành tiền chỉ tính phần còn lại.

Thực hiện trong OrderDetailsForm.cs và OrdersWorkspacePage.cs. Bổ sung trường đọc tùy chọn trong AdminOrderDetail và OrderAdministrationService để lấy thông tin nhận món/thanh toán từ SQL, không thay schema hoặc lệnh nghiệp vụ.

Kiểm tra: build; test API quản lý/nhân viên và WinForms PosSalesUiChecks mở Chi tiết bằng nút thật, xác nhận đúng bốn phần, không có tab kỹ thuật, chụp ảnh artifacts/tests/xunit/ADM01-05/<database>/POS-order-four-sections.png. Kết quả ở artifacts/test-results/basic-order-details.trx.
