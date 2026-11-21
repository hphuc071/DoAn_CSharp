# UC-ADM-04 Phiếu nhập xuất kho

Các lớp kiểm thử trong thư mục này dùng project xUnit chung. Danh mục nguồn và bộ lọc nằm trong tests/test-manifest.json.

## Mã kiểm thử

- [StockDocumentAdministrationTests.cs](StockDocumentAdministrationTests.cs)
- [StockDocumentEditorChecks.cs](StockDocumentEditorChecks.cs)

## Chạy kiểm thử

~~~powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-04"
~~~

Test API và giao diện sử dụng database thử riêng khi cần SQL. Kết quả đạt cần căn cứ lượt chạy và tệp TRX tương ứng.

## Kịch bản kiểm thử 06

### UC-ADM-04 — Tồn kho và chứng từ (tái sử dụng thư mục UC-ADM-04 cũ)

| Ca kiểm thử | Kết quả mong đợi |
|---|---|
| Lưu/sửa/xóa nháp nhập | Không đổi tồn, được sửa/xóa với đúng phiên bản |
| Xác nhận nhập, gửi lại cùng mã | Cộng đúng một lần, có sổ kho/audit/outbox |
| Sửa/xóa sau xác nhận | API409; trigger SQL chặn sửa dòng đã ghi sổ |
| Ngày tương lai (nhập và xuất) | Xác nhận SCHEDULED, khóa sửa/xóa, chưa đổi tồn; đến hạn worker POSTED đúng một lần |
| Đến hạn thiếu hàng | FAILED, lỗi rõ, không trừ một phần; không tự thử lại khi bổ sung hàng; sửa/xác nhận lại cùng ID |
| Nguyên liệu mới chưa có số dư | Lưu nháp không tạo số dư; xác nhận mới tạo và cộng tồn |
| Xuất vượt tồn ở một dòng | Rollback toàn phiếu, giữ nháp để sửa/xóa |
| Hủy phiếu đã ghi sổ | Tạo phiếu ngược, giữ gốc, chặn hủy hai lần |
| Kiểm kê/điều chỉnh cũ | Đọc lịch sử được; không sửa/xóa/xác nhận |
| Lập kiểm kê/điều chỉnh qua API cũ | Bị từ chối400; chỉ nhận Nhập/Xuất |
| Lỗi audit | Rollback trạng thái, tồn, sổ kho và outbox |
| Khách/ẩn danh | Không truy cập chức năng Admin |
| UI thật STA | 2 loại phiếu, chọn ngày, trường theo loại, thành tiền/tổng, lưu nháp không đổi tồn, thêm nhiều nguyên liệu, tự xóa ô nhập, chọn dòng sửa, chặn trùng không ghi đè, bỏ dòng và footer không bị cắt |
| Nâng cấp schema30 cũ | Giữ dữ liệu/rowversion phiếu nháp và đã chốt; thêm ngày nullable; chạy lại an toàn |

Các test chỉ chạy trên database GUID riêng. Không reset database quán.

| Hủy lịch | CANCELLED có audit, không tự ghi sổ khi đến hạn |
| Worker lỗi audit | Rollback tồn/sổ/trạng thái; phục hồi quyền thì ghi sổ đúng một lần |
| Nâng cấp lịch | Thêm lỗi nullable/check trạng thái, giữ phiếu/rowversion cũ, chạy lại an toàn |
| UI lịch | Xác nhận trong editor/danh sách; Đang xử lý khóa sửa, FAILED cho sửa, hủy lịch; polling giữ chọn/cuộn |
