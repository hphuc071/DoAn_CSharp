# UC-ADM-07 — Thống kê, báo cáo và nhật ký

## Phạm vi tinh gọn
- Lọc từ ngày đến ngày; nguồn Web/POS, hình thức nhận và ca là bộ lọc bổ sung.
- Ba chỉ tiêu: Tổng tiền đơn hoàn thành, Số đơn hoàn thành, Tiền đã hoàn.
- Ba bảng: Đơn hoàn thành / Món bán chạy / Tiền đã hoàn.
- Xuất Excel đúng snapshot đang xem; nhật ký chỉ tra cứu, xem chi tiết. Không quản lý tệp đã xuất, phân tích tỷ lệ phức tạp hoặc xuất PDF báo cáo.

## Cách tính
1. Đơn chỉ được tính khi COMPLETED và giờ hoàn thành thuộc khoảng đã chọn. Đơn đã thanh toán nhưng đang chờ/đang pha/đang giao chưa được cộng. Ngày tạo không quyết định kỳ doanh thu.
2. Tổng tiền lấy giá món/topping đã chốt × số ly phục vụ (đặt trừ hủy), cộng phí giao hiện hành, trừ giảm trừ khiếu nại hợp lệ nếu có lịch sử. Không lấy giá menu mới. Nguồn khiếu nại đã có chứng từ chỉ giảm một lần.
3. Món bán chạy nhóm theo mã món (gộp size/nóng lạnh), đếm ly thuộc đơn hoàn thành, bỏ ly hủy; tiền món gồm topping, không phân bổ phí giao/hoàn chung vào từng món.
4. Tiền đã hoàn chỉ cộng LanChiHoanTien.SUCCEEDED theo thời điểm có kết quả, gồm đơn hủy hoặc đơn của kỳ trước. Duyệt hủy/hạn mức chưa phải tiền đã trả; pending/failed/reconciling không cộng.
5. Lọc ca: đơn thuộc ca xử lý đơn; khoản hoàn thuộc ca thực sự chi. Không chuyển giao dịch tiền cũ sang ca mới. Hai chỉ tiêu tổng đơn và tiền hoàn được trình bày riêng, không trừ tiền hoàn lần thứ hai.
6. Giao diện ngày địa phương UTC+07, cận cuối bao gồm hết ngày bằng khoảng đầu-inclusive/cuối-exclusive. Kỳ không phát sinh hiển thị0 và bảng trống, lỗi truy vấn giữ báo cáo cũ kèm thông báo, không giả số0.

## Excel và nhật ký
- Snapshot nhất quán trong transaction, gắn người yêu cầu; job/receipt có mã chống trùng và tải lại kiểm quyền. Lưu Excel sau khi worker tạo xong; dữ liệu thay đổi sau snapshot không đổi workbook đó.
- Workbook5sheet: thông tin kỳ/định nghĩa, tổng hợp3chỉ tiêu và3bảng. Số tiền/số lượng là số, tên khách/món là text, không tạo công thức từ nội dung người dùng.
- Nhật ký tìm người/thao tác/ngày, phân trang50dòng, chỉ đọc. Hiện thời gian, người, thao tác, đối tượng, kết quả và chi tiết lý do; dữ liệu nhạy cảm trong snapshot được server che, không có sửa/xóa.
- Quyền ReportsRead/ReportsExport/AuditRead vẫn kiểm độc lập tại server. API tạo xuất mới chỉ EXCEL; PDF hóa đơn UC-ADM-06 giữ nguyên. Job/snapshot cũ còn lưu và được kiểm quyền khi đọc/tải, không xóa lịch sử.

## Triển khai
Tái sử dụng ReportsManagementPage/AuditManagementPage/ReportExportDialog, ReportAdministrationService/ReportDocumentWriter và thư mục test UC-ADM-10 cũ. Không thay database/schema, không reset dữ liệu. DTO KPI cũ giữ tương thích snapshot lịch sử; báo cáo mới dùng REPORT_SIMPLE và chỉ3chỉ tiêu trong phạm vi.