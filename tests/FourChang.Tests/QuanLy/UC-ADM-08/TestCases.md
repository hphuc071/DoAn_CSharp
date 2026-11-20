# UC-ADM-08 Nhân viên và ca làm việc

Các lớp kiểm thử trong thư mục này dùng project xUnit chung. Danh mục nguồn và bộ lọc nằm trong tests/test-manifest.json.

## Mã kiểm thử

- [EmployeeAdministrationTests.cs](EmployeeAdministrationTests.cs)
- [EmployeeUiTests.cs](EmployeeUiTests.cs)
- [WorkSessionAdministrationTests.cs](WorkSessionAdministrationTests.cs)
- [WorkSessionConcurrencyTests.cs](WorkSessionConcurrencyTests.cs)
- [WorkSessionUiTests.cs](WorkSessionUiTests.cs)

## Chạy kiểm thử

~~~powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-08"
~~~

Test API và giao diện sử dụng database thử riêng khi cần SQL. Kết quả đạt cần căn cứ lượt chạy và tệp TRX tương ứng.

## Kịch bản kiểm thử 11

### UC-ADM-08 — tái sử dụng UC-ADM-08/12
- Hồ sơ: tên trùng vẫn phân biệt ID; tìm/lọc/trang, lưu replay/stale/rollback, sửa địa chỉ không đổi snapshot ca; ngừng/chờ nhận bàn giao bị chặn, kích hoạt không mở lại ca.
- Ca: chỉ completed đúng ca hoàn tất, tiền/ngày snapshot; tiền thu/hoàn thuộc ca phát sinh, kiểm đếm khớp mới kết/chuyển. Chưa gửi không đổi thành0. Ca kết thúc hợp lệ không cần Admin duyệt lại.
- Bàn giao: đủ tập đơn/khoản chưa rõ + đúng phiên bản/mốc, replay1biên bản;2ca tranh người nhận chỉ1thành công, không khóa máy quán dùng chung. Người nhận được đổi máy nhưng phải đúng nhân viên.
- Toàn chuỗi: caA thu, caA bàn giao, caB pha/hoàn tất, caB có doanh thu/đơn, caA giữ tiền; báo cáo/chi tiết ca khớp. CaB tự kết thúc và không lặp Chiều trong ngày, Tối mở được không buộc giờ.
- Phục hồi: mã một lần/hết hạn/cấp lại, không chứa trong audit/receipt, không cấp quyền bán/Admin, gửi số đếm đúng rồi chốt ngoại lệ ca chưa kết thúc. Khóa tạm/tiếp tục cũ không tồn tại.
- UI:4nút nhân viên, lọc ngày/tên ca gọn, chi tiết3tab, không ô nhập tiền Admin hoặc mã kỹ thuật không cần thiết. Resize/footer/không chớp và giữ hồ sơ/ca đang chọn.

## Kịch bản kiểm thử 12

### UC-ADM-08 — tái sử dụng UC-ADM-11/12
- Hồ sơ: tên trùng vẫn phân biệt ID; tìm/lọc/trang, lưu replay/stale/rollback, sửa địa chỉ không đổi snapshot ca; ngừng/chờ nhận bàn giao bị chặn, kích hoạt không mở lại ca.
- Ca: chỉ completed đúng ca hoàn tất, tiền/ngày snapshot; tiền thu/hoàn thuộc ca phát sinh, kiểm đếm khớp mới kết/chuyển. Chưa gửi không đổi thành0. Ca kết thúc hợp lệ không cần Admin duyệt lại.
- Bàn giao: đủ tập đơn/khoản chưa rõ + đúng phiên bản/mốc, replay1biên bản;2ca tranh người nhận chỉ1thành công, không khóa máy quán dùng chung. Người nhận được đổi máy nhưng phải đúng nhân viên.
- Toàn chuỗi: caA thu, caA bàn giao, caB pha/hoàn tất, caB có doanh thu/đơn, caA giữ tiền; báo cáo/chi tiết ca khớp. CaB tự kết thúc và không lặp Chiều trong ngày, Tối mở được không buộc giờ.
- Phục hồi: mã một lần/hết hạn/cấp lại, không chứa trong audit/receipt, không cấp quyền bán/Admin, gửi số đếm đúng rồi chốt ngoại lệ ca chưa kết thúc. Khóa tạm/tiếp tục cũ không tồn tại.
- UI:4nút nhân viên, lọc ngày/tên ca gọn, chi tiết3tab, không ô nhập tiền Admin hoặc mã kỹ thuật không cần thiết. Resize/footer/không chớp và giữ hồ sơ/ca đang chọn.
