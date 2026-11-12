| Mã TC | Luồng đặc tả | Tình huống | Tiền điều kiện và dữ liệu | Các bước thực hiện | Kết quả mong đợi | BR |
| --- | --- | --- | --- | --- | --- | --- |
| TC-KH-WEB05-001 | Luồng chính | Lấy biến thể và tính tiền giỏ hàng | Món có các biến thể size, topping hợp lệ | 1. Gọi GET /api/menu/{id}/variants; 2. Gửi POST /api/customer/orders/quote | Trả về đầy đủ tùy chọn đường/đá/topping và tính đúng tổng tiền | BR-CART-02 |
## Bổ sung luồng bán hàng/thanh toán

ChooseNextProductBrowserTests kiểm tra focus cấu hình khi chọn món, xóa form sau thêm, giữ giỏ, không mang ghi chú/số lượng cũ sang món mới.
