# Mã đơn ngắn dùng chung

- Mẫu: WEB-1, WEB-2, WEB-125; POS-3. Không đệm số 0, không ngày/GUID trong mã hiển thị.
- Phần số là MaDonHang đã được SQL Server cấp. Đây là dãy ID chung, có thể có khoảng trống; không đếm MAX+1 trên client. Không đánh lại ID dữ liệu cũ, nên ID 20002 sẽ hiển thị WEB-20002.
- Quy ước duy nhất: src/Shared/FourChang.Presentation.Shared/Contracts/Common/OrderCode.cs. Format kiểm tra ID/kênh; TryParse hỗ trợ tìm mã ngắn không phân biệt hoa/thường.
- Đơn mới: tạo mã tạm TMP-... dài 28 ký tự (cột varchar(30)), nhận identity rồi lưu WEB-ID/POS-ID trong cùng transaction. Không trả/commit mã tạm. Mã hóa đơn tạo mới nhận mã đơn ngắn.
- Đơn cũ: giữ MaDonHienThi gốc trong database để tra cứu. DTO đơn trả mã ngắn suy từ ID/kênh, không cập nhật trực tiếp dòng cũ hoặc thay FK. Tìm kiếm hỗ trợ cả mã ngắn và mã cũ.
- Bản hóa đơn/PDF, tham chiếu ngân hàng và biên nhận chống trùng đã phát hành vẫn giữ nguyên. Gửi lại yêu cầu cũ trả đúng biên nhận gốc; trạng thái/mã trình bày hiện tại lấy qua endpoint đơn được phân quyền.
- Đã đồng bộ các màn hình đơn Web/POS/Admin, bảng tương thích, hồ sơ khách, ca làm và đối soát hết hạn. Không thêm bảng/cột, không thay thanh toán, kho hoặc quyền.
- Bảng đơn bỏ cắt mã bằng dấu ba chấm; mã in đậm, vạch ngang nhẹ, tiền căn phải, giữ chọn/cuộn khi cập nhật nền.

Kiểm thử C# xUnit: ShortOrderCodeFormatTests và ShortOrderCodeIntegrationTests; bổ sung kiểm tra mã lưu thực trong PosCheckoutTests và SimulationPaymentBrowserTests. Chạy kèm CancellationApproval, OrderListSeparation và CompactOrderToolbar. Kết quả: artifacts/test-results/short-order-codes.trx.