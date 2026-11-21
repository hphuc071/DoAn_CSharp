# UC-POS-03 Tiếp nhận và xử lý đơn hàng

Các lớp kiểm thử trong thư mục này dùng project xUnit chung. Danh mục nguồn và bộ lọc nằm trong tests/test-manifest.json.

## Mã kiểm thử

- [OrderListRefreshTests.cs](OrderListRefreshTests.cs)
- [StaffOrderWorkflowTests.cs](StaffOrderWorkflowTests.cs)

## Chạy kiểm thử

~~~powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-POS-03"
~~~

Test API và giao diện sử dụng database thử riêng khi cần SQL. Kết quả đạt cần căn cứ lượt chạy và tệp TRX tương ứng.

Các kịch bản dùng chung ở ../DungChung cũng được gắn nhãn UC theo từng phương thức.
