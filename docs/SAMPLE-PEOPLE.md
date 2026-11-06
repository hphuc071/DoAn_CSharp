# Dữ liệu mẫu nhân viên và khách hàng

Bộ nạp bổ sung 10 hồ sơ nhân viên và 10 tài khoản khách hàng bằng tên tiếng Việt. Mã nhân viên/khách hàng lấy từ khóa IDENTITY hiện có, không ép mã mới hoặc thêm MaNhanVienHienThi. Dữ liệu cũ giữ nguyên; tổng số hồ sơ sẽ gồm dữ liệu cũ cộng thêm bộ mẫu.

Chạy từ gốc đồ án:

```powershell
dotnet run --project src/Backend/FourChang.Api -- --seed-people
```

API không cần đang chạy; SQL Server cần hoạt động và .env đã cấu hình. Lệnh kiểm tra schema trước khi nạp. Chạy lại không tạo trùng bộ mẫu. Không gọi qua HTTP, không thêm bảng và không sửa dữ liệu giao dịch. Nếu định danh mẫu xung đột hồ sơ khác thì dừng và rollback thay vì ghi đè.

| STT | Nhân viên | Khách hàng | Tên đăng nhập khách |
|---|---|---|---|
| 1 | Nguyễn Hoàng Minh | Nguyễn Tuấn Kiệt | mau.kh01 |
| 2 | Trần Ngọc Anh | Trần Bảo Ngọc | mau.kh02 |
| 3 | Lê Đức Huy | Lê Minh Quân | mau.kh03 |
| 4 | Phạm Khánh Linh | Phạm Phương Thảo | mau.kh04 |
| 5 | Hoàng Gia Bảo | Hoàng Nhật Nam | mau.kh05 |
| 6 | Vũ Thanh Hà | Vũ Quỳnh Chi | mau.kh06 |
| 7 | Đặng Quang Phúc | Đặng Hải Đăng | mau.kh07 |
| 8 | Bùi Thảo Vy | Bùi Ngọc Hân | mau.kh08 |
| 9 | Đỗ Minh Khang | Đỗ Quốc Bảo | mau.kh09 |
| 10 | Ngô Diệu Nhi | Ngô Thanh Trúc | mau.kh10 |

Tên và thông tin liên hệ được tạo để minh họa, không phải thông tin người dùng thực. Email dùng miền example.test. Không đưa mật khẩu mẫu dùng chung lên GitHub: mật khẩu ban đầu ngẫu nhiên, chỉ lưu bản băm. Khi muốn thử đăng nhập, Admin đặt lại mật khẩu tạm tại Khách hàng, sau đó khách đổi mật khẩu theo luồng hiện có. Nhân viên không được tạo tài khoản, chỉ dùng mã nội bộ và họ tên để mở ca trên máy được phép.

Kết quả lệnh liệt kê mã thật được cấp trong database máy đang chạy. Các thành viên có thể có mã khác nhau nếu dữ liệu cũ khác nhau; API dùng mã thực nhận từ máy chủ, không hard-code số mã từ máy người khác.

## Thay toàn bộ bộ mẫu về mã 1–10

Theo yêu cầu làm sạch dữ liệu cũ, dự án bổ sung lệnh bảo trì riêng. Dừng API/Desktop và kiểm tra không có đơn, thanh toán, hóa đơn hoặc giao dịch liên quan trước khi dùng:

```powershell
dotnet run --project src/Backend/FourChang.Api -- --reset-people-samples --confirm-database FourChangDb
```

Lệnh sao lưu COPY_ONLY có CHECKSUM vào volume SQL Server, chạy VERIFYONLY rồi mới thay dữ liệu. Nếu có tham chiếu giao dịch ngoài ba nhóm, lệnh dừng và không xóa giao dịch. Nhân viên, khách hàng, ca cũ cùng các phiên khách hàng/địa chỉ/nhật ký thuộc nhóm này được thay trong một transaction; Admin, thực đơn, kho và các nhóm khác giữ nguyên. Không tắt khóa ngoại và không thay schema.

Sau thay: nhân viên 1–10, khách hàng 1–10, ca 1–10 với mã ca CA0001–CA0010. Ca số i gắn nhân viên số i, tên lúc mở ca được chốt trong audit. Cả 10 ca là ca lịch sử CLOSED, tiền đầu/dự kiến/thực đếm đều 500.000đ; chưa tạo khoản thu/hoàn giả. Các lần mở ca thật sau đó dùng nhân viên hiện có trên thiết bị được cấp. Lần thêm hồ sơ/ca tiếp theo nhận mã 11 (SQL Server có thể bỏ khoảng ID sau lỗi/khởi động lại theo cơ chế IDENTITY).

Đường dẫn backup và danh sách mã/tên thực tế được xuất ở kết quả lệnh. Dữ liệu mẫu khách hàng có mật khẩu ngẫu nhiên được băm; dùng Admin cấp lại mật khẩu tạm nếu cần đăng nhập. File .bak nằm trong Docker volume, không đưa lên GitHub.

Riêng trigger TR_NhatKyHeThong_BatBien được tạm tắt trong transaction lúc xóa nhật ký mẫu thuộc ba nhóm, bật lại ngay trước các bước tiếp theo. Rollback bảo toàn dữ liệu và trạng thái bảo vệ. Không thay định nghĩa trigger; các trigger và khóa ngoại khác vẫn hoạt động.

## Bổ sung địa chỉ mẫu

File docs/database/35-bang/sample-contact-addresses.sql điền địa chỉ liên hệ còn trống của 10 nhân viên và tạo địa chỉ giao hàng mặc định cho 10 khách mẫu chưa có sổ địa chỉ. Đối chiếu bằng tên/số liên hệ hoặc tên đăng nhập, không hard-code mã. Giữ địa chỉ đã nhập, không sửa đơn/ca, không gán tọa độ giả; chạy lại không tạo trùng. Địa chỉ được tạo để minh họa đồ án. Kết quả trên máy hiện tại lưu tại artifacts/sample-contact-addresses.json.

