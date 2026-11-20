# UC-ADM-05 Đơn hàng và yêu cầu hủy

Các lớp kiểm thử trong thư mục này dùng project xUnit chung. Danh mục nguồn và bộ lọc nằm trong tests/test-manifest.json.

## Mã kiểm thử

- [OrderAdministrationTests.cs](OrderAdministrationTests.cs)

## Chạy kiểm thử

~~~powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-05"
~~~

Test API và giao diện sử dụng database thử riêng khi cần SQL. Kết quả đạt cần căn cứ lượt chạy và tệp TRX tương ứng.

## Kịch bản kiểm thử 07

### UC-ADM-05 — tái sử dụng thư mục UC-ADM-05
- Admin không được gọi ACCEPT/START/FINISH/DELIVER/COMPLETE/CANCEL/RETRY_DELIVERY/EXPIRE; đơn/kho/version không đổi.
- Nhân viên gửi yêu cầu đúng món/số ly; chưa duyệt chưa hủy.
- Duyệt hoặc từ chối có phiên bản/mã chống trùng; không chi tiền sớm, không tạo hồ sơ hoàn cho đơn chưa thu tiền.
- Giữ kết quả đã từ chối/đã duyệt, lọc tại máy chủ trước phân trang; đọc địa chỉ snapshot.
- Giao thất bại chỉ cần lý do, không hồi kho, replay không lặp.
- WinForms: Admin chỉ xem/ngoại lệ được phép; nút duyệt/từ chối và trạng thái rõ, resize không cắt bộ lọc.
- Hồi quy chuỗi Khách → Nhân viên → Quản lý → Nhân viên hoàn tại quầy và tiến trình pha chế POS.
