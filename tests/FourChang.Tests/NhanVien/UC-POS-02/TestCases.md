# UC-POS-02 Lập đơn bán hàng tại quầy

Các lớp kiểm thử trong thư mục này dùng project xUnit chung. Danh mục nguồn và bộ lọc nằm trong tests/test-manifest.json.

## Mã kiểm thử

- [PosCheckoutTests.cs](PosCheckoutTests.cs)
- [PosSalesUiTests.cs](PosSalesUiTests.cs)

## Chạy kiểm thử

~~~powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-POS-02"
~~~

Test API và giao diện sử dụng database thử riêng khi cần SQL. Kết quả đạt cần căn cứ lượt chạy và tệp TRX tương ứng.

Các kịch bản dùng chung ở ../DungChung cũng được gắn nhãn UC theo từng phương thức.

# Test case

## Bổ sung luồng bán hàng/thanh toán

PosCheckoutTests kiểm tra nhiều món/cấu hình, thiếu tiền/đổi giá, CASH, giữ BOM, replay, QR GET không ghi, xác nhận đồng thời, hết hạn, rollback audit và phân quyền. PosSalesUiTests bấm control thật để thêm hai món/thu tiền và kiểm nút nằm trong cửa sổ 1440/1280.
