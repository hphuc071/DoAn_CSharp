# Quy tắc mở và kết thúc ca hiện tại

## Thay đổi đã chốt

- Bỏ giới hạn một ca OPEN/LOCKED trên mỗi nhân viên hoặc máy.
- Bỏ giới hạn một lần Sáng/Chiều/Tối trong ngày. Không kiểm tra khung giờ thực tế.
- Mỗi thao tác mở mới tạo mã ca, phiên ký và sổ tiền riêng. Không tự đóng, xóa hoặc gộp ca cũ.
- Vẫn đối chiếu đúng mã/họ tên nhân viên đang làm; kiểm tra tên ca và tiền đầu ca hợp lệ.
- Tiếp tục ca đã lưu vẫn mở đúng ID của phiên, không tự chọn ca bất kỳ theo tên/máy.
- Các biên bản bàn giao đã chốt vẫn phải tiếp nhận đúng người/máy theo nghiệp vụ bàn giao hiện có.

## Tránh tạo trùng khi mất phản hồi

Desktop gửi `OpenShift.MaThaoTac` (GUID mới cho mỗi lần mở chủ động). Khi chưa nhận được kết quả, giữ nguyên GUID và nội dung để thử lại. API trả lại đúng mã ca từ audit của thao tác; cùng GUID nhưng đổi nội dung trả 409. Không ghi token vào audit. Client cũ không gửi GUID vẫn được hỗ trợ; client tích hợp mới nên gửi GUID.

## Database và tích hợp

Chạy `--init-db` bằng cấu hình quản trị hiện có để áp dụng `007-independent-work-sessions.sql`. Script chỉ đổi hai index UX_Ca_MayDangMo/UX_Ca_NhanVienDangMo thành index thường IX_Ca_MayDangMo/IX_Ca_NhanVienDangMo, có thể chạy lại an toàn. Không thêm/bớt bảng, cột, FK hoặc sửa dữ liệu ca. EF, schema.sql và model.json cùng được cập nhật.

## Kết thúc ca

Nút “Gửi kết quả & kết thúc ca” vẫn lưu thực đếm, dự kiến, chênh lệch/lý do và audit nguyên tử rồi đóng đúng ca. Sau khi máy chủ xác nhận, POS quay về mở ca. Điều kiện chặn đóng khi còn đơn dở/khoản tiền chưa rõ và luồng gửi đối chiếu/bàn giao được giữ nguyên.

## Kiểm thử

`IndependentShiftTests`: nhiều ca cùng nhân viên/máy/ngày, lặp tên ca, ca trước LOCKED, đóng riêng và giữ tiền ca khác; index không unique; mở đồng thời khác mã và gửi lặp cùng mã. Chạy kèm `EmployeeIdentityTests`, `FinishShiftTests`, `WorkSessionAdministrationTests` để kiểm tra tương thích danh tính, kết thúc ca và bàn giao.
