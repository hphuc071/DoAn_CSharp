# Nhận món mô phỏng — tại bàn, mang đi, giao hàng

Phạm vi đã được người dùng chốt: giao hàng mô phỏng cố định 15.000đ/đơn; tại bàn chọn từ danh sách bàn CSDL; không bổ sung cấu hình nóng cho Trà đào/Cold brew.

## Cấu hình và tích hợp
- `Orders__FulfillmentSimulationEnabled=true` chỉ có hiệu lực trong Development; local .env đã bật. .env.example giữ false. Không đưa .env lên Git.
- Web và API cần cùng cờ. scripts/Start-LocalWeb.ps1 chỉ truyền cờ được phép, không chuyển bí mật SQL.
- Không thay số bảng, schema hoặc dữ liệu công thức. Giữ DTO tạo đơn hiện có, thêm DTO/GET danh sách bàn chỉ gồm mã hiển thị và tên, không trả hash QR.
- SimulatedFulfillment.cs tập trung phí và kiểm tra hình thức/bàn/địa chỉ. Món và topping vẫn lấy giá chung trong CSDL. Không dùng GPS, khoảng cách client hay API bản đồ cho mô phỏng.
- Dùng lại giao dịch tạo đơn, giữ kho BOM, audit/outbox, idempotency, chấp thuận tổng tiền và yêu cầu thanh toán. Giao hàng lưu địa chỉ/ghi chú/phí cùng snapshot chính sách SIMULATION; tại bàn lưu FK và mã bàn đã chốt.
- Kiểm tra bàn đang sử dụng trong giao dịch Serializable khi tạo đơn. Hết hàng, sai giá, bàn dừng hoặc thiếu địa chỉ không tạo đơn dở dang.
- Các đơn là dữ liệu chung cho API nhân viên/quản lý; quy trình xử lý trạng thái và thu tiền giữ nguyên. Không tự xác nhận tiền hoặc biến QR mô phỏng thành VietQR thật.

## Giao diện
- Người nhận/điện thoại dùng chung; chọn hình thức hiện đúng trường bàn hoặc địa chỉ/ghi chú.
- Đổi hình thức/bàn/địa chỉ/ghi chú làm mất xác nhận tổng tiền và lấy lại báo giá. Gửi lại thao tác chưa rõ kết quả vẫn dùng payload cũ.
- Nút xác nhận có tên Đặt tại bàn/Đặt mang đi/Đặt giao hàng. Tiền giao do backend tính, không hardcode giá món ở giao diện.
- Size theo S/M/L. Món chỉ có một nhiệt độ còn bán tự chọn nhiệt độ đó; các món có Nóng/Lạnh giữ cả hai. Không sinh BOM mới.

## Kiểm tra
- Build: 0 warning, 0 error.
- 37 test hồi quy đạt ở simulated-fulfillment.trx; test end-to-end ban đầu phát hiện proxy test thiếu header chống trùng, đã sửa allowlist hai header cần thiết.
- Luồng ba hình thức đạt sau sửa proxy (simulated-fulfillment-e2e.trx).
- Lượt cuối 7 test đạt (simulated-fulfillment-final.trx): ba hình thức và QR mô phỏng, kiểm tra mọi cấu hình có bán, nóng/lạnh và đi Thanh toán, gate Production/cờ tắt, bỏ phí giao, khoảng cách client, thiếu địa chỉ và bàn bị dừng sau báo giá.
- SQL test dùng database fixture riêng; không sửa tồn kho, giá hoặc công thức của dữ liệu local khi kiểm thử.
