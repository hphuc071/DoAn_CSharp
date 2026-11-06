# Kiểm thử UC ADM 13

| ID | Tình huống | Kết quả | Cách kiểm |
|---|---|---|---|
| 13-01 | Lọc nhân viên/ngày/tên/trạng thái/máy; ngày ngược | Đúng phạm vi; 400 với bộ lọc sai | API + WorkforceUiTests |
| 13-02 | Chưa có thực đếm | Chặn, NULL không thành 0 | WorkSessionAdministrationTests |
| 13-03 | CASH, CK, hoàn CASH thành công, hoàn CK/FAILED | Chỉ CASH VERIFIED và CASH SUCCEEDED vào quỹ | WorkSessionAdministrationTests |
| 13-04 | Gửi số lệch thiếu lý do | 400 | WorkSessionAdministrationTests |
| 13-05 | Gửi đối chiếu, lặp cùng mã, tra receipt LOCKED | Một hiệu ứng; không cho ca khóa bán | WorkSessionAdministrationTests |
| 13-06 | Admin chốt, chốt lặp, mã mới chốt lại | CLOSED; receipt cũ; 409 khi chốt lại | WorkSessionAdministrationTests |
| 13-07 | Audit lỗi khi chốt/bàn giao | Rollback, ca nguồn còn nguyên | WorkSessionAdministrationTests |
| 13-08 | Đơn dở/khoản chưa rõ | Chặn chốt, yêu cầu xử lý/bàn giao | WorkSessionAdministrationTests |
| 13-09 | Bỏ sót nguồn bàn giao hoặc ca đích xung đột | 409, không đổi ca nguồn | WorkSessionAdministrationTests |
| 13-10 | Bàn giao, POS nhận đúng người/máy | Liên kết ca trước, giao dịch/nhân viên nguồn giữ nguyên | WorkSessionAdministrationTests |
| 13-11 | Người nhận chờ bàn giao bị ngừng | Chặn để giữ liên kết hợp lệ | WorkSessionAdministrationTests |
| 13-12 | Khôi phục LOCKED, thử mở lại CLOSED | Có lý do, đếm lại; CLOSED bị chặn | WorkSessionAdministrationTests |
| 13-13 | Xem/bỏ xác nhận hoặc danh sách rỗng | Không ghi ca hoặc giả dữ liệu | Thủ công UI; API chỉ đọc |
| 13-14 | Mất mạng trước gửi/sau commit | Không báo thành công giả; receipt và cùng GUID | Receipt API/gửi lặp + mô phỏng mạng thủ công |
| 13-15 | Nhân viên nhận INACTIVE/máy REVOKED | 400, giữ ca nguồn | WorkSessionAdministrationTests |

Thực hiện UI: Admin > Nhân viên > Lịch sử ca, hoặc Admin > Ca làm việc > Xem đối chiếu. POS > Ca làm việc > nhập thực đếm, xác nhận kiểm đếm, Gửi đối chiếu. Dữ liệu tiền nguồn phải đến từ UC sở hữu, không nhập giả khi vận hành.
