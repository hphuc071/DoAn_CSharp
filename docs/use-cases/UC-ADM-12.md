# UC-ADM-12 — Quản lý ca làm việc

Nguồn: `dac-25-Uc.docx` do người dùng cung cấp; giữ nguyên nội dung đặc tả.

## Tác nhân chính

Quản lý

## Mục tiêu

Giám sát và tra cứu ca; kiểm tra thực đếm, chênh lệch và khoản chưa rõ; xác nhận chốt/bàn giao hoặc hỗ trợ khôi phục ca theo điều kiện. Nhân viên vẫn tự nhập mã/họ tên và chọn ca tại UC-POS-01; không có quản trị xác thực thiết bị.

## Tiền điều kiện

Quản lý có phiên hợp lệ; có quyền xem/xử lý ca; API hoạt động. Xác nhận chốt/bàn giao cần ca hiện hữu, phiên bản mới nhất và các dữ liệu đối chiếu bắt buộc.

## Kích hoạt

Quản lý mở Ca làm việc hoặc chọn Lịch sử ca từ hồ sơ nhân viên tại UC-ADM-11.

## Luồng chính

1. Quản lý mở danh sách ca. Hệ thống kiểm tra quyền, hiển thị các ca với mã ca, nhân viên, ca Sáng/Chiều/Tối, mã máy lịch sử nếu có, thời gian và trạng thái.

2. Quản lý lọc theo nhân viên, khoảng ngày hợp lệ nhập trực tiếp, tên ca, trạng thái hoặc mã máy lịch sử nếu có. Hệ thống trả danh sách phân trang đúng bộ lọc; giữ MaNhanVien để phân biệt người trùng tên.

3. Quản lý chọn một ca. Hệ thống tải chi tiết, phiên bản, giao dịch tiền mặt/chuyển khoản, đơn đang xử lý và thông tin bàn giao liên quan.

4. Hệ thống tổng hợp tiền mặt đầu ca, thu CASH đã xác minh, hoàn CASH thành công và tính tiền dự kiến; hiển thị thực đếm, chênh lệch/lý do nếu đã nhập. Quản lý đối chiếu với giao dịch nguồn; CK được hiển thị riêng.

5. Quản lý xem nhật ký ca, đơn và giao dịch liên quan để kiểm tra. Hệ thống cho truy vết đúng quyền, không sửa giao dịch nguồn hoặc tạo khoản điều chỉnh tiền chỉ vì xem đối chiếu.

6. Quản lý kết thúc tra cứu. Hệ thống giữ bộ lọc; ca và số liệu chốt không thay đổi nếu chỉ xem. Khi cần xử lý chốt hoặc bàn giao, chuyển sang luồng thay thế tương ứng.

## Luồng thay thế

A1. Kiểm tra và xác nhận chốt ca — sau bước 4, ca đã Gửi đối chiếu ở UC-POS-01, đang LOCKED và đã có tiền thực đếm. Quản lý kiểm tra lý do nếu lệch, đơn còn dở và khoản chưa rõ. Hệ thống yêu cầu xử lý hết hoặc bàn giao có liên kết; tải lại số tiền nguồn trước xác nhận. Quản lý xác nhận; hệ thống lưu CLOSED, thời điểm kết thúc, người xác nhận, dự kiến/thực đếm/lý do và nhật ký nguyên tử, vô hiệu khả năng ghi mới của ca. Nếu nhân viên đã chốt hợp lệ tại UC-POS-01, chỉ kiểm tra dữ liệu đã chốt, không chốt lần hai.

A2. Xác nhận bàn giao — sau bước 3–4, cần đổi người hoặc máy. Quản lý chọn nhân viên nhận đang làm, thông tin nơi nhận nếu cần ghi lịch sử, nhập lý do và xác định đơn/khoản chưa rõ cần bàn giao. Hệ thống kiểm tra không có ca đích xung đột, giữ nguyên giao dịch tài chính của ca nguồn; lưu HANDED_OVER, người xác nhận, thời điểm và nhật ký. Ca nhận được mở theo UC-POS-01 và liên kết ca trước; các đơn đang xử lý được truy vết theo bàn giao. Không đổi MaNhanVien của ca nguồn hoặc chuyển các khoản thu cũ sang ca mới.

A3. Xử lý chênh lệch — sau bước 4, thực đếm khác dự kiến. Quản lý xem chứng cứ và yêu cầu bổ sung lý do; thiếu thực đếm thì yêu cầu nhân viên nhập tại UC-POS-01, không tự coi NULL là 0. Có lỗi thu/hoàn thì chuyển đúng nghiệp vụ UC-ADM-09 hoặc UC-POS-05 trước khi tính lại; không sửa/xóa khoản nguồn để làm hết lệch. Quay về A1 khi điều kiện đủ.

A4. Khôi phục sau sự cố — sau bước 3, máy mất điện/khởi động lại hoặc ca bị khóa. Quản lý kiểm tra ca, phiên ca và nhân viên khai báo; cho tiếp tục ca hợp lệ tại UC-POS-01 hoặc thực hiện A2 nếu đổi người hoặc ngữ cảnh làm việc. Không tạo ca trùng, không mở lại ca CLOSED để ghi thêm tiền.

A5. Hủy xác nhận hoặc danh sách rỗng — Quản lý bỏ thao tác trước khi lưu thì hệ thống giữ trạng thái cũ. Nếu bộ lọc không có ca, hiển thị rỗng hợp lệ và cho đổi bộ lọc; không tạo ca mẫu.

A6. Ca khóa tạm và ca chờ đối chiếu

Điểm rẽ nhánh: bước 3. Hệ thống phân biệt lý do khóa từ lịch sử và dữ liệu ca; không coi mọi LOCKED đều là yêu cầu đối chiếu. Nếu nhân viên chưa nhập thực đếm/gửi đối chiếu, Quản lý yêu cầu thực hiện UC-POS-01, không thay NULL bằng 0 để chốt. Khôi phục ca khóa tạm cần kiểm tra phiên; ca chờ đối chiếu không tự mở bán lại trước xử lý của Quản lý. Giữ nguyên tiền nguồn và phiên bản khi chỉ xem.

## Luồng ngoại lệ

E1. Tại bước 2, khoảng ngày sai hoặc bộ lọc không hợp lệ: chỉ rõ tiêu chí, giữ thông tin đã nhập; không trả số liệu suy đoán.

E2. Tại bước 3 hoặc A1/A2, ca đã chốt/bàn giao hoặc phiên bản thay đổi: tải lại; thao tác cùng mã đã thành công trả kết quả cũ, không chốt hay bàn giao hai lần.

E3. Tại A1/A3, thiếu thực đếm, tiền âm hoặc chênh lệch chưa có lý do: chặn xác nhận, chỉ rõ thông tin cần bổ sung; không ghi tiền mặc định để chốt.

E4. Tại A1/A2, còn đơn dở hoặc giao dịch chưa rõ chưa được xử lý/bàn giao: chặn kết thúc, liệt kê nguồn; không giả hoàn tất đơn hoặc giả đã xác minh tiền.

E5. Tại A2, nhân viên nhận ngừng làm, phiên ca nhận không hợp lệ hoặc ca đích xung đột: từ chối bàn giao, giữ ca nguồn và yêu cầu chọn lại; kiểm tra đồng thời ở máy chủ.

E6. Tại mọi bước, phiên Quản lý hết hạn/không có quyền: từ chối và yêu cầu xác thực lại; không dùng ca POS để phê duyệt.

E7. Khi tải hoặc xác nhận mất mạng/timeout: dữ liệu cũ có nhãn thời điểm, chặn chốt/bàn giao offline; tra trạng thái/mã thao tác khi nối lại trước thử tiếp.

E8. Khi lưu ca, liên kết bàn giao hoặc nhật ký thất bại: hủy toàn giao dịch; không đóng ca nguồn một phần hoặc để ca nhận ghi đè lịch sử. Giao dịch ngân hàng thực tế vẫn cần đối soát, không bị đảo bằng rollback CSDL.

## Hậu điều kiện

Tra cứu thành công trả đúng nhân viên/ca/bộ lọc và số liệu nguồn. Chốt/bàn giao hợp lệ lưu trạng thái, thời điểm, người xác nhận và dấu vết đầy đủ; ca kết thúc không nhận lệnh mới. Giao dịch nguồn và lịch sử nhân viên giữ nguyên. Thất bại không chốt/bàn giao một phần hoặc mở ca trùng.

## Quy tắc nghiệp vụ

BR-05, BR-06, BR-08, BR-09, BR-14, BR-19, BR-29, BR-33, BR-36

## Bổ sung đã được người dùng duyệt

Mất hẳn phiên POS: Admin cấp mã phục hồi dùng một lần, chỉ cho nhân viên gửi thực đếm/đối chiếu; không mở quyền bán hoặc tự chốt. Sau đó Admin kiểm tra rồi chốt/bàn giao. Giữ `DELIVERY_FAILED` trong database để nhận diện đơn giao thất bại kết thúc khi đối chiếu.
