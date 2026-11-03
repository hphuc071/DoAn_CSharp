# Mẫu test case dùng chung

Sao chép mẫu này vào đúng `tests/<Actor>/<MaUC>/TestCases.md` khi triển khai UC. Giữ mã UC theo đặc tả 26 UC; mỗi tình huống kiểm thử chỉ là TC, không tạo thêm UC.

| Thông tin | Nội dung cần điền |
| --- | --- |
| Mã và tên UC | Mã/tên đã chốt trong đặc tả |
| Actor sở hữu | Khách hàng / Nhân viên / Quản lý |
| Quy tắc áp dụng | Mã BR hiện có liên quan trực tiếp |
| Môi trường | API/Web/Desktop; phiên bản build, database test và cổng |
| Tiền điều kiện chung | Dữ liệu, trạng thái, quyền/phiên cần có |
| Lớp C# thực thi | Đường dẫn tương đối tới file test trong UC |

| Mã TC | Luồng đặc tả | Tình huống | Tiền điều kiện và dữ liệu | Các bước thực hiện | Kết quả mong đợi | BR |
| --- | --- | --- | --- | --- | --- | --- |
| TC-<actor>-<UC>-<loại>-001 | Luồng chính / A… / E… | Tên tình huống cụ thể | Dữ liệu sinh lúc chạy, trạng thái trước thử | 1. Chuẩn bị; 2. Thao tác; 3. Đối chiếu | Quan sát/trạng thái/lỗi/HTTP cụ thể; dữ liệu được giữ khi lỗi | BR-… |

## Ghi nhận mỗi lần chạy

| Mã TC | Thực tế | Trạng thái | Bằng chứng | Ghi chú lỗi |
| --- | --- | --- | --- | --- |
| Mã TC tương ứng | Kết quả quan sát | Chưa chạy / Đạt / Không đạt / Bị chặn | Đường dẫn kết quả dưới artifacts/tests | Mô tả và mã lỗi nếu có |

Không ghi mật khẩu/token/.env vào dữ liệu minh họa hoặc log. Test phá lỗi phải chạy trên database tạm, khôi phục quyền/cấu hình trong finally. Luồng chưa có chức năng không được đánh dấu Đạt chỉ vì chưa chạy.
