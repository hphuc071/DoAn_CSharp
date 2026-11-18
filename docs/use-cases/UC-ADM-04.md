# UC-ADM-04 — Quản lý tồn kho, nhập và xuất nguyên liệu

Phạm vi cập nhật theo quyết định 18/11/2026: bỏ Kiểm kê, giữ các luồng nhập/xuất và lịch sử.

## Chức năng
- Xem tồn thực tế, giữ chỗ, khả dụng và cảnh báo; không sửa số tồn trực tiếp.
- Phiếu nhập: ngày nhập, nguồn nhập/ghi chú tùy chọn, nhiều nguyên liệu g/ml, số lượng, đơn giá tùy chọn, thành tiền và tổng tiền.
- Phiếu xuất: ngày xuất, hỏng/hết hạn, lý do và nhiều nguyên liệu/số lượng. Tiêu hao pha chế vẫn tự động theo công thức.
- Không lập/sửa/xác nhận/xóa phiếu kiểm kê hoặc điều chỉnh cũ. Giữ schema và dữ liệu để tra cứu lịch sử; không xóa hay chuyển loại phiếu cũ.

## Lập phiếu dễ sử dụng
1. Chọn Nhập kho hoặc Xuất kho, ngày và thông tin tương ứng.
2. Chọn nguyên liệu, nhập số lượng và giá (nếu nhập kho). Thành tiền dòng cập nhật ngay.
3. Thêm vào phiếu: dòng xuất hiện trong bảng, ô số lượng/giá trả về 0 để nhập dòng tiếp theo.
4. Muốn sửa: chọn dòng trong bảng, sửa rồi bấm Cập nhật dòng. Nhập dòng mới thoát chế độ sửa. Bỏ dòng chỉ tác động nháp.
5. Không âm thầm ghi đè nguyên liệu đã có: báo trùng và yêu cầu chọn dòng để sửa.
6. Lưu nháp chưa đổi tồn. Danh sách tự chọn phiếu vừa lưu, mở rộng ngày lọc khi cần.

## Xác nhận và bảo toàn lịch sử
- Ngày quá khứ/hôm nay/tương lai được lưu riêng với giờ ghi sổ. Ngày tương lai chưa được xác nhận; đến ngày vẫn cần xác nhận thủ công.
- Admin xác nhận khi hàng thực nhập/xuất. API kiểm quyền, phiên bản, nguyên liệu đang dùng và tồn không giữ chỗ.
- Tồn/sổ kho/audit/outbox cùng transaction, lỗi một dòng rollback toàn phiếu. Gửi lại cùng mã không cộng/trừ lần nữa.
- Xác nhận thành công: khóa sửa/xóa phiếu và chi tiết. Sửa sai bằng Hủy phiếu đã ghi sổ tạo chứng từ ngược có lý do; không sửa/xóa gốc hay hủy hai lần.
- Thiếu tồn: giữ nháp để sửa/xóa, không tự thử lại. Nhập nguyên liệu mới chưa có TonKho chỉ tạo số dư khi xác nhận lần đầu.
- Lọc ngày luôn áp dụng (mặc định30 ngày), không checkbox. Nhãn Nháp/Thành công/Đã hủy rõ ràng.

## Triển khai
Tái sử dụng StockDocumentsPage, StockDocumentEditorForm, StockDocumentService, client/DTO và thư mục test UC-ADM-06 cũ. Không thay schema hay dữ liệu quán khi bỏ Kiểm kê. Các cột kiểm kê trong DTO/schema chỉ giữ tương thích lịch sử.
