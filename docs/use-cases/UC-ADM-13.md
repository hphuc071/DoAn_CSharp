# UC-ADM-13 Quản lý ca làm việc

Nguồn: đặc tả 26 UC, UC-ADM-13; liên kết ADM-12/POS-01. Có hai quyết định người dùng đã chấp thuận: thêm Gửi đối chiếu tại POS (lưu số đếm và LOCKED), và giữ mã database DELIVERY_FAILED để ánh xạ đơn kết thúc thất bại trong đối chiếu. Không đổi schema hoặc tự sửa trạng thái/tiền của đơn.

## Đối chiếu đặc tả

| Nhánh | Hiện thực | Quy tắc |
|---|---|---|
| Chính 1–2 | Danh sách phân trang, lọc MaNhanVien/ngày/tên ca/trạng thái/máy | BR-06, BR-08, BR-09 |
| Chính 3–6 | Chi tiết, snapshot tên mở ca, thu/hoàn nguồn tách CASH/CK, đơn dở, khoản chưa rõ, nhật ký, thời điểm dữ liệu; xem không ghi | BR-19, BR-29, BR-36 |
| A1 | Chốt từ thực đếm đã lưu, tính lại nguồn trong transaction, chặn đơn dở/khoản chưa rõ, CLOSED/EndAt/Admin/audit | BR-09, BR-14, BR-19, BR-33 |
| A2 | Chọn người đang làm/máy ACTIVE, lý do và tập nguồn khớp; HANDED_OVER; POS mở ca nhận sau và gắn MaCaBanGiaoTruoc | BR-08, BR-09, BR-14, BR-33 |
| A3 | NULL khác 0; lệch cần lý do; cho tiếp tục ca khóa để nhân viên kiểm đếm/bổ sung lại; nguồn thu/hoàn xử lý tại UC sở hữu | BR-19, BR-33, BR-36 |
| A4 | Cho tiếp tục ca LOCKED đúng người/máy hợp lệ có lý do; không mở CLOSED/HANDED_OVER; số đếm trước vẫn trong audit | BR-08, BR-09, BR-29, BR-33 |
| A5 | Rỗng hợp lệ; hủy form không gọi ghi | BR-05 |
| E1–E8 | Kiểm tra bộ lọc, phiên bản, thực đếm/lý do, tập nguồn, ca nhận, quyền; receipt và rollback nguyên tử | BR-05, BR-08, BR-14, BR-29 |

## Công thức và truy vết

Tiền dự kiến = đầu ca + ThanhToan CASH/VERIFIED.SoTienDaXacMinh − LanChiHoanTien CASH/SUCCEEDED.SoTien. Chuyển khoản và hoàn thất bại không vào quỹ. Chênh lệch = thực đếm − dự kiến; NULL chưa nhập không thành 0. Nguồn tiền chỉ của chính ca; đơn/khoản chưa rõ đi theo chuỗi ca nguồn để không bị mất khi bàn giao nhiều lần.

Tên ca cũ lấy từ OPEN_SHIFT.TenNguoiThucHienDaChot, không đổi theo hồ sơ mới. Dữ liệu cũ thiếu audit được ghi rõ “hồ sơ hiện tại”, không bịa snapshot.

Biên bản bàn giao lưu bền vững ở NhatKyHeThong, action SHIFT_HANDED_OVER, JSON HandoverInfo và receipt. Khóa toàn vẹn theo transaction; ca nguồn kết thúc đồng thời với audit. Người nhận và máy được giữ cho biên bản chưa nhận; việc mở ca POS tiêu thụ biên bản qua MaCaBanGiaoTruoc. Không tạo ca nhận thay nhân viên, không chuyển các khoản thu/hoàn cũ, không đổi MaNhanVien ca nguồn. Chốt thành công chặn các lệnh Staff vì middleware kiểm tra OPEN mỗi lần.

## Tích hợp

- GET `/api/admin/work-sessions` và `/{id}`: Admin đọc danh sách/chi tiết.
- POST `/{id}/close`, `/{id}/handover`, `/{id}/resume`: Admin, GUID, rowversion và TienMatDuKienDaXem từ chi tiết mới nhất bắt buộc; nguồn tiền thay đổi trả 409.
- GET `/api/admin/work-sessions/operations/{guid}`: tra kết quả cùng người thực hiện.
- GET/POST `/api/pos/shifts/current/reconciliation`: đúng token ca + X-POS-Key + X-POS-Machine; ca LOCKED chỉ đọc/tra receipt, không sửa số đã gửi.
- GET `/api/pos/shifts/current/reconciliation/operations/{guid}`: phục hồi mất phản hồi sau gửi đối chiếu.
- POS mở ca vẫn dùng `/api/pos/shifts`; tự gắn ca nguồn khi khớp người/máy nhận. Chưa nhận đúng thì 409; không mở ca thay thế tùy ý.

Shared/WorkSessions/ShiftReconciliation.cs là bộ đọc dùng chung, HandoverLink.Ancestors là điểm nối cho POS-03 sau này. Các chức năng xử lý đơn/thu tiền/hoàn tiền chưa triển khai không được giả hoàn tất; ca còn nguồn chưa rõ sẽ bị chặn chốt và có thể bàn giao hợp lệ.
