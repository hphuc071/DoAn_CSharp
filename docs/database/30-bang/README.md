# Database hiện hành: 30 bảng

Schema SQL Server và EF Core cùng ánh xạ **30 bảng nghiệp vụ, 402 thuộc tính, 63 khóa ngoại, 11 trigger**. Bảng `dbo.sysdiagrams` do SSMS tạo được giữ nguyên và không tính là bảng nghiệp vụ. Không tạo `__EFMigrationsHistory` hoặc view giả tên bảng đã bỏ.

- `schema.sql`: tạo database rỗng 30 bảng.
- `model.json`: manifest cột, kiểu dữ liệu, khóa và ràng buộc.
- `008-simplify-to-30.sql`: chuyển dữ liệu 37 → 30 trong một transaction; có thể chạy lại.
- `012-simplify-columns.sql`: chuyển schema30 từ 423 xuống 402 thuộc tính; backup trước nâng cấp, lưu giá trị cột cũ vào nhật ký, rollback toàn bộ nếu lỗi.
- `TU-DIEN-DU-LIEU.md`: danh sách bảng và thuộc tính đang lưu.

## Thay đổi

| Bảng cũ | Cách thay thế |
| --- | --- |
| ThietBiPOS | Bỏ cấp/thu hồi máy. Nhân viên mở ca bằng mã và họ tên; quyền ghi cần phiên ca do server ký. |
| DanhMucMon | Lưu `Mon.LoaiMon`: Cà phê, Trà, Đá xay. Mã 1/2/3 trong DTO chỉ là lựa chọn cố định. |
| DonViTinh | Lưu `NguyenLieu.DonViCoSo`: g hoặc ml. Mã 1/2 trong DTO không phải FK. |
| QuyDoiDonViNguyenLieu | Nhập trực tiếp g/ml; bỏ hệ số quy đổi. |
| CauHinhMonThem | Topping đang dùng áp dụng cho mọi cấu hình món; giới hạn đọc từ MonThem. |
| TuyChonPhaCheHopLe | Đường/đá: 0, 25, 50, 75, 100. Nóng luôn đá 0. Mặc định đường 50, đá 50 nếu lạnh. |
| GiuChoNguyenLieu | Gộp lượng giữ/tiêu hao/giải phóng, hạn và trạng thái vào NguyenLieuChiTietDonHang. |

Cột bỏ: tọa độ địa chỉ khách; tọa độ/khoảng cách/hạn báo phí của đơn; mã băm/phiên bản/hạn QR bàn; lý do chênh lệch ca; tiền khách đưa; mã đơn vị nhập/hệ số trên chi tiết phiếu kho. Mã FK danh mục/đơn vị được thay bằng giá trị trực tiếp như bảng trên.

Vẫn giữ tên/đơn vị/giá/định mức snapshot, khoản thu, khoản hoàn, số tiền kỳ vọng và thực đếm, rowversion, khóa chống trùng, nhật ký và các thời điểm nghiệp vụ. Chúng phục vụ đối soát, bảo vệ lịch sử và xử lý đồng thời. Các trường lịch/banner đang có chức năng thật vẫn được giữ.

## Chạy và nâng cấp

Mở Docker Desktop (Linux containers), sau đó tại thư mục repo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ChuanBi-Database.ps1
dotnet run --project src/Backend/FourChang.Api -- --check-db
dotnet run --project src/Backend/FourChang.Api -- --test-db
```

Bootstrap nhận diện và kiểm tra đầy đủ schema cũ trước nâng cấp. Database 34/35 được đi qua các bản nâng cấp lịch sử; database 37 được sao lưu native bằng `COPY_ONLY,CHECKSUM`, chạy `RESTORE VERIFYONLY`, rồi mới chuyển 30. Backup tự động nằm trong `/var/opt/mssql/data/*_before30_*.bak` trên SQL container. Schema lạ hoặc dữ liệu không chuyển được sẽ dừng; không tự đoán đơn vị/danh mục hoặc xóa bản ghi.

Nên đóng ứng dụng khi nâng cấp. Không chạy trực tiếp schema rỗng lên database đang có dữ liệu. Không dùng `EnsureDeleted`, `EnsureCreated`, xóa volume Docker hoặc reset dữ liệu để nâng cấp.

Nếu cần phục hồi, dùng bản `.bak` vào database riêng để đối chiếu trước; phục hồi schema 37 phải đi cùng phiên bản code cũ. Không ghi đè database đang chạy chỉ để thử restore.

## Nghiệp vụ được giữ

- Giá cấu hình size/nóng lạnh và topping do server tính; đơn cũ giữ nguyên giá đã chốt.
- Topping vẫn phải có BOM hiệu lực và đủ nguyên liệu. Thiếu tồn không biến thành còn hàng.
- Mỗi nhân viên làm Sáng/Chiều/Tối một lần trong ngày UTC+07, không ép giờ thực tế. Đơn và tiền vẫn thuộc đúng ca.
- Đơn hoàn thành mới tính tổng hoàn thành. Tiền kiểm đếm đối chiếu với quỹ tiền mặt, không dùng doanh số chuyển khoản thay tiền mặt.
- Hủy: khách yêu cầu → nhân viên gửi → quản lý duyệt → nhân viên hoàn tiền tại quầy. Gửi yêu cầu không tự hủy hoặc tự chi tiền.
- QR thanh toán/giao hàng/chọn bàn vẫn là mô phỏng theo cấu hình Development; không coi việc xem QR là đã trả tiền.
- Điện thoại khách và người nhận đúng 10 chữ số; mật khẩu tối thiểu 5 ký tự.

## Kiểm thử

`Schema30MigrationTests` dựng schema37 có đơn, hóa đơn, thanh toán và giữ chỗ trong database GUID riêng; kiểm chuyển đổi, chạy lại, bảo toàn tiền, đọc catalog và bất biến snapshot. `DatabaseTests` kiểm database rỗng và nâng cấp từ34 có sơ đồ SSMS. Các test POS, BOM, kho, hủy/hoàn, ca và báo cáo dùng schema30 do bootstrap tạo.

Các thư mục 34-bang, 35-bang, 37-bang là tài liệu/migration lịch sử, không dùng chúng làm schema hiện hành.

## Nâng cấp mức đường mặc định của công thức — 15/11/2026

Migration009-recipe-default-sugar.sql thêm CongThuc.PhanTramDuongMacDinh int NOT NULL DEFAULT50, CHECK0/25/50/75/100; không thêm bảng. Bootstrap kiểm manifest30 cũ, sao lưu native COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY trước nâng cấp rồi kiểm manifest mới. Database mới tạo thẳng schema hiện hành; nâng lại không thay dữ liệu. TyLeHaoHut giữ cho lịch sử, công thức mới/sửa dùng0; đơn mới trừ lượng thực dùng lúc bắt đầu pha, hoàn thành không trừ thêm.

Database thật đã nâng cấp và kiểm đúng30 bảng/64FK. Đối chiếu số dòng/checksum dữ liệu cả30 bảng trước/sau (bỏ rowversion và cột mới) không có khác biệt. Bản sao lưu tại artifacts/database/recipe-defaults-before.bak; đối chiếu tại recipe-defaults-before.txt và recipe-defaults-after.txt cùng thư mục. Không reset khách hàng, đơn hoặc lịch sử.

## Tinh gọn thuộc tính — 21/11/2026

Giữ 30 bảng; bỏ 21 thuộc tính và FK tự tham chiếu `DonHang.MaDonLienQuan`. Danh sách chính xác ở `COLUMN-SIMPLIFICATION.md`. Bootstrap kiểm schema cũ, backup native COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY trước migration 012; backup nằm trong SQL container với tên `*_beforeColumnSimplification_*.bak`. Không chạy schema rỗng trên dữ liệu hiện hữu.

DTO cũ của nguyên liệu/công thức/phiếu kho còn trường tương thích: tồn an toàn và hao hụt trả 0, mốc/số liệu kiểm kê cũ trả null. Chúng không còn là cột SQL. Giá trị trước nâng cấp được lưu trong NhatKyHeThong với HanhDong=SCHEMA_COLUMNS_ARCHIVE, MaDoiTuong=012, tra theo LoaiDoiTuong và khóa bản ghi trong DuLieuTruocJson. Lịch sử hóa đơn JSON, giá/định mức đơn, tiền, tồn và khóa chống gửi trùng được giữ.
