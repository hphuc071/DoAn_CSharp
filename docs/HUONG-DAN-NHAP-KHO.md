# Nhập hàng vào kho

## Quy trình trên giao diện

1. Mở **Chứng từ kho → Lập phiếu** và chọn **Nhập kho**.
2. Điền nhà cung cấp/nguồn nhập nếu có, nhập **Lý do lập phiếu**.
3. Chọn nguyên liệu. Nhập số lượng theo đơn vị ghi cạnh ô:
   - Hạt cà phê dùng **g**: nhận 2 kg thì nhập `2000`.
   - Sữa dùng **ml**: nhận 2 lít thì nhập `2000`.
4. Đơn giá là giá cho **1 g hoặc 1 ml**. Ví dụ cà phê 100.000 đồng/kg tương ứng `100` đồng/g. Chưa ghi nhận giá có thể để `0` theo quy tắc hiện có.
5. Bấm **Thêm vào phiếu**. Lặp lại với nguyên liệu khác. Chọn một dòng trong bảng để nạp lại dữ liệu lên ô nhập; đổi số lượng rồi bấm Thêm vào phiếu để cập nhật, không tạo trùng dòng.
6. Bấm **Lưu phiếu nháp**. Danh sách tự chọn phiếu vừa lưu và nhắc bước tiếp theo. Lúc này tồn kho **chưa tăng**.
7. Kiểm tra phiếu đang chọn, bấm **Ghi sổ**, nhập/xác nhận lý do. Chỉ khi thành công, hệ thống cập nhật tồn kho.
8. Mở **Tồn kho**, bấm **Tải lại** để kiểm tra kết quả.

Ví dụ tồn trước là 10.000 g, nhập 2.000 g: lưu nháp vẫn 10.000 g; ghi sổ thành công là 12.000 g nếu không có phát sinh khác.

## Những điểm đã chỉnh

- Trường theo loại phiếu: nhập có nhà cung cấp và đơn giá; xuất có mục đích xuất; kiểm kê/điều chỉnh có hướng dẫn nhập số tồn thực tế.
- Hiển thị đơn vị ngay cạnh số lượng, ví dụ kg→g/lít→ml và giá theo từng đơn vị cơ sở.
- Ẩn mã kỹ thuật, ẩn cột tồn đối chiếu khi nhập/xuất; định dạng số dễ đọc.
- Chặn thêm dòng nhập/xuất bằng 0 ngay tại giao diện, vẫn cho thực đếm bằng 0 khi kiểm kê đúng quy tắc API.
- Dòng đã chọn có thể nạp lại để sửa. Giữ Lưu/Đóng ngoài vùng cuộn.
- Nháp thành công tự chọn đúng phiếu trong danh sách; thông báo rõ chưa cập nhật kho, còn bước Ghi sổ.
- Không đổi schema, API hoặc tính toán tồn kho; vẫn giữ cơ chế chống ghi lặp/tra kết quả sau mất phản hồi.

## Source và test

- `src/Presentation/FourChang.Desktop/Admin/StockDocuments/StockDocumentEditorForm.cs`
- `src/Presentation/FourChang.Desktop/Admin/StockDocuments/StockDocumentsPage.cs`
- `tests/FourChang.Tests/QuanLy/UC-ADM-06/StockDocumentEditorChecks.cs`
- `tests/FourChang.Tests/QuanLy/DungChung/AdminFeaturesUiTests.cs` (gọi bộ kiểm tra editor theo UC).

Kiểm thử dùng SQL/API fixture riêng; không đưa hàng mẫu hoặc sửa tồn của database quán.

Lệnh chạy:

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-06|FullyQualifiedName~OperationsUiTests"
```

Kết quả: build toàn solution 0 warning/0 error; 11/11 test đạt trong stock-editor-usability-final.trx. Đã xem ảnh editor thật ở kích thước đầy đủ và cửa sổ nhỏ, sửa lỗi hàng nhà cung cấp bị cắt.

[Ảnh biểu mẫu mới](previews/stock-entry-editor.png).
