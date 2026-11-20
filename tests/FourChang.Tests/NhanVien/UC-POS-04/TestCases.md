# UC-POS-04 Yêu cầu và xử lý duyệt hủy món

Các lớp kiểm thử trong thư mục này dùng project xUnit chung. Danh mục nguồn và bộ lọc nằm trong tests/test-manifest.json.

## Mã kiểm thử

- [CancellationApprovalTests.cs](CancellationApprovalTests.cs)
- [CancellationDesktopTests.cs](CancellationDesktopTests.cs)

## Chạy kiểm thử

~~~powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-POS-04"
~~~

Test API và giao diện sử dụng database thử riêng khi cần SQL. Kết quả đạt cần căn cứ lượt chạy và tệp TRX tương ứng.

Các kịch bản dùng chung ở ../DungChung cũng được gắn nhãn UC theo từng phương thức.
