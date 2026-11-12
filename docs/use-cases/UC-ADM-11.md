# UC-ADM-11 — Quản lý danh sách nhân viên

Nguồn: `dac-25-Uc.docx` do người dùng cung cấp; giữ nguyên nội dung đặc tả.

## Tác nhân chính

Quản lý

## Mục tiêu

Quản lý hồ sơ nhân viên để POS đối chiếu mã và họ tên khi mở ca; thêm, xem, sửa, tìm, ngừng/kích hoạt, xóa có điều kiện và tra lịch sử ca. Có địa chỉ liên hệ; không tạo tài khoản, mật khẩu hoặc mã hiển thị riêng.

## Tiền điều kiện

Quản lý có phiên hợp lệ và quyền quản lý nhân viên; API/CSDL hoạt động. Sửa/ngừng hồ sơ cần bản ghi hiện hữu và phiên bản mới nhất.

## Kích hoạt

Quản lý mở Danh sách nhân viên và chọn Thêm nhân viên.

## Luồng chính

1. Quản lý mở Danh sách nhân viên. Hệ thống kiểm tra phiên, tải danh sách có phân trang gồm mã nội bộ, họ tên, số liên hệ, địa chỉ liên hệ, ngày vào làm và trạng thái.

2. Quản lý chọn Thêm nhân viên. Hệ thống hiển thị biểu mẫu hồ sơ; không hiển thị username, mật khẩu hoặc MaNhanVienHienThi.

3. Quản lý nhập họ tên, số điện thoại và địa chỉ liên hệ nếu có, ngày vào làm và trạng thái đang làm. Hệ thống kiểm tra trường bắt buộc, độ dài và định dạng; tên trùng không tự coi là cùng một người.

4. Quản lý kiểm tra và xác nhận lưu. Hệ thống kiểm tra quyền và mã chống trùng, sinh MaNhanVien nội bộ, lưu hồ sơ cùng nhật ký trong một giao dịch.

5. Hệ thống trả hồ sơ đã lưu và phiên bản. Quản lý xem kết quả; nhân viên đang làm được dùng để đối chiếu mã và họ tên khi mở ca của UC-POS-01.

## Luồng thay thế

A1. Tra cứu và xem hồ sơ — tại bước 1, Quản lý nhập tên, số liên hệ hoặc chọn trạng thái. Hệ thống tìm kiếm/phân trang; Quản lý mở hồ sơ, hệ thống trả thông tin và phiên bản hiện tại. Không có kết quả là trạng thái rỗng hợp lệ.

A2. Sửa hồ sơ — từ hồ sơ tại A1, Quản lý sửa thông tin và xác nhận. Hệ thống kiểm tra dữ liệu/phiên bản, lưu cùng audit, trả bản mới; các giao dịch và thông tin đã chốt trong lịch sử không bị đổi theo.

A3. Ngừng làm hoặc kích hoạt lại — từ A1, Quản lý chọn trạng thái và nhập lý do. Hệ thống kiểm tra ca chưa kết thúc. Nếu còn ca thì yêu cầu xử lý ở UC-ADM-12 trước; nếu đủ điều kiện thì lưu trạng thái, giữ lịch sử. Hồ sơ ngừng làm không mở ca mới; kích hoạt lại không khôi phục ca đã đóng.

A4. Xem lịch sử ca — từ chi tiết hồ sơ, Quản lý chọn Lịch sử ca. Hệ thống chuyển sang UC-ADM-12 với bộ lọc MaNhanVien đã chọn; cho quay lại hồ sơ. Không tự mở/chốt ca khi xem lịch sử.

A5. Hủy thao tác trước lưu — Quản lý đóng biểu mẫu, xác nhận bỏ thay đổi nếu cần; hệ thống không lưu hồ sơ hoặc cập nhật dở dang. Quay về danh sách.

A6. Xóa hồ sơ chưa có tham chiếu

Điểm rẽ nhánh: từ hồ sơ tại A1. Quản lý chọn Xóa và xác nhận. Máy chủ kiểm tra lại không có ca, giao dịch hoặc tham chiếu bắt buộc và phiên bản chưa đổi; đủ điều kiện mới xóa cùng audit. Đã có lịch sử thì chặn xóa cứng, hướng dẫn A3 ngừng làm, không đổi khóa hoặc tái gán ca lịch sử cho người khác. Kết quả trả danh sách cập nhật; lỗi đồng thời theo E3/E6/E7.

## Luồng ngoại lệ

E1. Tại bước 3–4 hoặc A2, thiếu họ tên/ngày vào làm, dữ liệu quá giới hạn hoặc số liên hệ sai định dạng: chỉ rõ trường lỗi, giữ nháp hợp lệ; không lưu.

E2. Tại bước 4 hoặc A2, hai người cùng tên hoặc tên đã tồn tại: không áp đặt duy nhất cho họ tên. Hiển thị thông tin phân biệt để Quản lý kiểm tra; chỉ từ chối khi có ràng buộc dữ liệu thực sự bị vi phạm, không gộp hồ sơ theo tên.

E3. Tại A2/A3, phiên bản đã thay đổi hoặc hồ sơ không còn tồn tại: từ chối ghi đè, tải lại để Quản lý xác nhận. Hồ sơ có lịch sử không xóa cứng; xử lý bằng ngừng làm.

E4. Tại A3, nhân viên còn ca chưa kết thúc: chặn ngừng làm, dẫn tới UC-ADM-12 để chốt/bàn giao có lịch sử; không tự đổi nhân viên phụ trách ca cũ.

E5. Tại mọi bước bảo vệ, phiên hết hạn hoặc không đủ quyền: từ chối; yêu cầu đăng nhập Quản lý. Tên nhân viên/ca không thay xác thực Admin.

E6. Khi tải/lưu bị mất mạng hoặc timeout: giữ nháp không nhạy cảm, không báo thành công; tra mã thao tác/phiên bản trước thử lại. Lặp cùng yêu cầu đã lưu trả hồ sơ cũ, không tạo nhân viên thứ hai.

E7. Khi ghi hồ sơ hoặc nhật ký thất bại: hủy toàn giao dịch, giữ dữ liệu trước đó; không để hồ sơ có mà thiếu lịch sử bắt buộc.

## Hậu điều kiện

Thành công: hồ sơ có khóa nội bộ, thông tin và trạng thái hợp lệ, nhật ký/phiên bản đầy đủ; đối chiếu mã/họ tên tại POS dùng trạng thái mới. Không sinh tài khoản hoặc mã hiển thị riêng. Thất bại: dữ liệu và lịch sử cũ được bảo toàn.

## Quy tắc nghiệp vụ

BR-05, BR-06, BR-08, BR-09, BR-13, BR-14, BR-28, BR-29, BR-33

## Bổ sung đã được người dùng duyệt

Chặn ngừng/xóa nhân viên đang chờ nhận bàn giao; cấp DELETE riêng `dbo.NhanVien` cho ứng dụng để thực hiện xóa chưa tham chiếu. API vẫn kiểm tra Admin, phiên bản, tham chiếu và ghi audit nguyên tử.
