# Mở ca bằng mã và họ tên

Theo yêu cầu cập nhật: nhân viên nhập MaNhanVien hiện có và họ tên. Không tạo tài khoản nhân viên, không thêm cột hoặc bảng.

| Tình huống | Kết quả mong đợi |
|---|---|
| Mã/tên đúng, nhân viên ACTIVE, máy được cấp | Xác minh thành công, mở đúng ca |
| Hai hồ sơ trùng tên | Phân biệt bằng MaNhanVien, không tự lấy người đầu tiên |
| Khác chữ hoa/thường hoặc khoảng trắng thừa | Vẫn nhận đúng tên; giữ nguyên dấu tiếng Việt |
| Mã đúng nhưng sai tên, mã lạ hoặc INACTIVE | Không xác minh; không tạo ca |
| Bỏ mã hoặc họ tên | Báo thiếu trường bắt buộc |
| Đã kiểm tra rồi thay mã/tên | Giao diện bỏ kết quả cũ; API mở ca kiểm tra lại |
| Máy thiếu hoặc bị thu hồi cấu hình | Không tra danh tính/mở ca; báo nhờ Quản lý kiểm tra thiết bị |
| Thử lại mở ca đúng người/máy | Giữ cùng ca đang mở, không tạo trùng |
| Nhập mã/tên rồi gọi Admin | Vẫn chỉ có quyền Staff |

Chạy `dotnet test FourChang.sln --filter "UseCase=UC-POS-01"` từ gốc dự án. Test dùng database tạm; không lưu DPAPI hoặc cấu hình POS thật. Test WinForms tạo ảnh mở ca, lỗi tên, xác minh, POS và Admin. Web có kiểm tra desktop/mobile trong bộ AccountAccess. Đây là cập nhật nhận diện trong POS-01, không đánh dấu các UC bán hàng chưa làm là đã hoàn thành.
