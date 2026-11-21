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
- Ngày nhập/xuất được lưu riêng với giờ ghi sổ. Xác nhận cho ngày quá khứ/hôm nay ghi sổ ngay; xác nhận ngày tương lai khóa phiếu ở Đang xử lý, chưa đổi tồn. Đến ngày UTC+07, Backend tự ghi sổ; khởi động lại vẫn xử lý lịch đã quá hạn.
- Admin xác nhận khi hàng thực nhập/xuất. API kiểm quyền, phiên bản, nguyên liệu đang dùng và tồn không giữ chỗ.
- Tồn/sổ kho/audit/outbox cùng transaction, lỗi một dòng rollback toàn phiếu. Gửi lại cùng mã không cộng/trừ lần nữa.
- Xác nhận thành công: khóa sửa/xóa/hủy phiếu và chi tiết. Endpoint hủy phiếu đã ghi sổ cũ trả409; lịch sử phiếu đảo cũ giữ nguyên.
- Xác nhận ngay nhưng thiếu tồn: giữ nháp để sửa/xóa. Phiếu hẹn đến hạn nhưng thiếu tồn: Cần kiểm tra, lưu nguyên nhân, không trừ dòng nào và không tự thử lại; Quản lý sửa rồi xác nhận lại hoặc hủy có audit. Nhập nguyên liệu mới chưa có TonKho chỉ tạo số dư khi xác nhận lần đầu.
- Lọc ngày luôn áp dụng (mặc định30 ngày), không checkbox. Trạng thái: Nháp / Đang xử lý / Cần kiểm tra / Thành công / Đã hủy. Polling nền chỉ bind khi dữ liệu đổi, giữ chọn/cuộn. Phiếu Đang xử lý bị khóa sửa/xóa; có Hủy phiếu chờ, không đổi tồn.

## Triển khai
Tái sử dụng StockDocumentsPage, StockDocumentEditorForm, StockDocumentService, client/DTO và thư mục test UC-ADM-06 cũ. Không thay schema hay dữ liệu quán khi bỏ Kiểm kê. Các cột kiểm kê trong DTO/schema chỉ giữ tương thích lịch sử.

## Xác nhận ngay khi lập phiếu — cập nhật19/11/2026
- Footer có Lưu phiếu nháp, Xác nhận phiếu và Đóng. Dòng đang nhập được thêm/cập nhật trước lưu, không bị bỏ sót khi bấm xác nhận trực tiếp.
- Xác nhận lưu nháp rồi ghi sổ đúng ID/phiên bản vừa trả về. Nếu ghi sổ thất bại, thông báo rõ nháp đã lưu, giữ ID để sửa/thử lại, không tạo phiếu mới.
- Sau thành công danh sách tự chọn phiếu và hiển thị Thành công; khóa sửa/xóa. Vẫn xác nhận được từ danh sách.
- Ngày tương lai được xác nhận ngay, hiện Đang xử lý; giữ mã thao tác khi timeout, không gửi phiếu mới theo suy đoán. Chỉ hiện Thành công sau khi kho/sổ/audit/outbox thực sự commit.


## Hẹn ngày tự ghi sổ — quyết định cập nhật19/11/2026
- Trạng thái lưu: DRAFT → SCHEDULED → POSTED; lỗi nghiệp vụ đến hạn chuyển FAILED; hủy lịch/chờ kiểm tra chuyển CANCELLED. Nháp không bao giờ tự ghi sổ.
- StockDocumentScheduleWorker đọc lịch mỗi5giây theo ngày UTC+07, nhập đến hạn cùng ngày trước xuất; xử lý từng phiếu trong transaction Serializable với khóa quản trị chung, đọc lại trạng thái trước ghi. Không ghi sổ hai lần khi nhiều worker hoặc khởi động lại.
- ApplyCore kiểm toàn bộ nguyên liệu/giữ chỗ/tồn trước khi sửa bất cứ dòng nào. Lỗi nghiệp vụ: FAILED, không biến động kho; lỗi hạ tầng/audit: rollback và giữ SCHEDULED để phục hồi khi dịch vụ hoạt động lại.
- FAILED cho sửa và xác nhận lại cùng ID; lưu sửa đưa về DRAFT. CANCELLED giữ phiếu/audit, không xóa lịch sử hoặc sinh bút toán kho.
- Schema30 giữ30bảng64FK, thêm PhieuKho.LoiXuLy nvarchar(500) NULL và mở CK_PhieuKho_2. Migration011 idempotent, backup native+verify trước ALTER; không thay dữ liệu/chứng từ cũ. Tác nhân tự ghi sổ là SYSTEM, người Admin đã duyệt giữ tại MaQuanLyDuyet/audit.

## Lịch sử kho tinh gọn — 19/11/2026
- Thay màn hình Truy vết bằng Lịch sử kho: tên nguyên liệu, đơn vị, tồn thực tế, đang giữ và khả dụng ở trên.
- Một bảng5cột: Thời gian / Hoạt động / Số lượng thay đổi / Nguồn / Ghi chú. Số lượng có dấu tăng/giảm, g/ml; ngày theo UTC+07.
- Không hiển thị các tab chi tiết giữ chỗ/BOM hoặc các cột mã kỹ thuật; dữ liệu và cách tính bên dưới giữ nguyên. Không có biến động: Chưa phát sinh nhập, xuất hoặc pha chế.

## Phiếu thành công không được hủy — cập nhật19/11/2026
- Phiếu POSTED/Thành công chỉ được xem, không sửa/xóa/hủy. Bỏ nút Hủy phiếu đã ghi sổ; endpoint reverse cũ luôn trả409, không tạo phiếu ngược/biến động kho hoặc phát lại hủy cũ.
- Chỉ SCHEDULED/Đang xử lý và FAILED/Cần kiểm tra được Hủy phiếu chờ. Lịch sử các phiếu đảo đã có vẫn giữ nguyên.
- Chi tiết phiếu nhập/xuất được sắp xếp gọn: tiêu đề và trạng thái, mã phiếu, ngày thực hiện/ghi sổ, nguồn nhập khi có và ghi chú, bảng nguyên liệu căn đúng cột. Nhập có đơn giá/thành tiền/tổng; xuất bỏ trường giá và nguồn nhập. Cửa sổ chỉ có Đóng, hỗ trợ thu nhỏ.
