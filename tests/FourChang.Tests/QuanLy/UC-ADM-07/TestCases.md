# UC-ADM-07 Báo cáo và nhật ký

Các lớp kiểm thử trong thư mục này dùng project xUnit chung. Danh mục nguồn và bộ lọc nằm trong tests/test-manifest.json.

## Mã kiểm thử

- [ReportAdministrationTests.cs](ReportAdministrationTests.cs)
- [ReportNavigationUiTests.cs](ReportNavigationUiTests.cs)

## Chạy kiểm thử

~~~powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-07"
~~~

Test API và giao diện sử dụng database thử riêng khi cần SQL. Kết quả đạt cần căn cứ lượt chạy và tệp TRX tương ứng.

## Kịch bản kiểm thử 10

### UC-ADM-07 — tái sử dụng thư mục UC-ADM-07
- Đã trả tiền nhưng chưa hoàn thành không tính; đúng kỳ hoàn thành mới tính. Giá snapshot, phí giao và giảm trừ hợp lệ; bỏ ly hủy khỏi tổng đơn/top món.
- Tiền hoàn chỉ khi chi thành công, đúng ngày/ca chi thực tế; không nhầm ca gốc hoặc duyệt hủy là đã trả. Replay payout không tăng số hai lần.
- Kỳ trống0/bảng trống; bộ lọc sai400; Customer403. Nhật ký không có route sửa/xóa và che mật khẩu/token/tài khoản.
- Excel5sheet, số có kiểu numeric, không formula, đúng snapshot trước khi phát sinh đơn mới; chống gửi trùng job. Tạo PDF báo cáo bị chặn; không ảnh hưởng PDF hóa đơn.
- UI3card/3tab, không PDF/tệp đã xuất; resize không cắt bộ lọc/KPI, số tiền lớn không bị che. Nhật ký chỉ đọc. Ba tab điều hướng đúng trang kể cả không chọn dòng.
