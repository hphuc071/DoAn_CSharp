# Tinh chỉnh POS và thao tác pha chế — 13/11/2026

Giữ nhận diện 4CHANG LINH NGU LAM, màu nâu/kem, sidebar, luồng thu tiền và định mức API.

## Thay đổi
- POS chọn kích cỡ, nóng/lạnh, đường và đá riêng. Nóng ẩn đá và dùng mức đá 0; lạnh chỉ hiện cặp đường/đá hợp lệ. Không tạo nhiệt độ hoặc giá ngoài dữ liệu API.
- Topping/số lượng/ghi chú bố trí gọn, tổng tiền và nút thêm ở footer cố định. Đổi size/nhiệt độ giữ lựa chọn còn hợp lệ; server kiểm tra lại trước lưu.
- Polling menu 5 giây chỉ dựng lại thẻ khi dữ liệu thay đổi; không xóa/dựng lại ảnh, danh mục, bàn hoặc binding giỏ khi không cần. Giữ bộ lọc và bàn đang chọn.
- Bắt đầu pha, pha xong và hoàn tất bỏ ô lý do. Server tự ghi mô tả vào nhật ký. Hủy và xử lý ngoại lệ vẫn bắt buộc lý do/chứng cứ như trước.
- Chọn dòng món bằng tên/size/nhiệt độ; tự điền toàn bộ số ly còn cần xử lý. Bắt đầu = đặt - hủy - đã bắt đầu; pha xong = đã bắt đầu - đã xong. Giữ khả năng xử lý một phần, không vượt số ly còn lại.
- Hộp thoại dùng tiếng Việt, bỏ nhãn START và trường ngoại lệ không liên quan, giảm khoảng trống.

## Các file thực hiện
- src/Presentation/FourChang.Desktop/Staff/Sales/PosItemEditorForm.cs
- src/Presentation/FourChang.Desktop/Staff/Sales/PosSalesPage.cs
- src/Presentation/FourChang.Desktop/Staff/Sales/PosDraftCart.cs
- src/Presentation/FourChang.Desktop/Shared/Orders/OrdersWorkspacePage.cs
- src/Presentation/FourChang.Desktop/Shared/Controls/Admin/AdminCommandDialog.cs
- src/Backend/FourChang.Api/Shared/Orders/OrderWorkflowService.cs
- tests/FourChang.Tests/QuanLy/DungChung/PosSalesUiChecks.cs
- tests/FourChang.Tests/NhanVien/Orders/StaffOrderWorkflowTests.cs

## Kiểm chứng
Build không lỗi/cảnh báo. 10 test liên quan đã đạt trên SQL/API và WinForms thật (pos-preparation-refinement.trx).
- Theo dõi hơn hai chu kỳ menu: timestamp thay đổi nhưng cùng thẻ/nút, không dispose.
- Thử nóng/lạnh, size riêng và ẩn/hiện cả trường đá; chụp ảnh form thật.
- Thêm hai món, mỗi món hai ly; thu tiền thật qua API trong database test, xác nhận giỏ được dọn sau thành công.
- Pha, pha xong và hoàn tất qua dialog thật; mặc định hai ly và không có ô lý do.
- Kiểm tra nhật ký vẫn có lý do tự ghi, replay không trừ kho hai lần, hủy không có lý do bị từ chối.
- Kiểm tra bảng đơn không rebind/nhấp nháy khi polling, vẫn nhận dữ liệu mới và giữ lựa chọn/cuộn.

Ảnh kiểm tra nằm trong artifacts/tests/xunit/ADM01-05/<database test>/POS-*.png.
Không đổi schema database, hợp đồng giá, logic thu/hoàn tiền hoặc phân quyền. Cần đóng/mở lại POS để nạp DLL mới.