# UC-POS-01 Nhận diện nhân viên và quản lý ca

Các lớp kiểm thử trong thư mục này dùng project xUnit chung. Danh mục nguồn và bộ lọc nằm trong tests/test-manifest.json.

## Mã kiểm thử

- [EmployeeIdentityTests.cs](EmployeeIdentityTests.cs)
- [EntryUiTests.cs](WinForms/EntryUiTests.cs)
- [FinishShiftTests.cs](FinishShiftTests.cs)
- [IndependentShiftTests.cs](IndependentShiftTests.cs)

## Chạy kiểm thử

~~~powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-POS-01"
~~~

Test API và giao diện sử dụng database thử riêng khi cần SQL. Kết quả đạt cần căn cứ lượt chạy và tệp TRX tương ứng.
