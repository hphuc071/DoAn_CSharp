# Database hiện hành — 35 bảng

Giữ nguyên 34 bảng nghiệp vụ đã chốt. Bổ sung duy nhất `ThietBiPOS` theo xác nhận của người dùng cho UC-ADM-02. `dbo.sysdiagrams` là bảng hỗ trợ SSMS, được giữ nguyên và không tính vào 35 bảng nghiệp vụ.

## Tệp thiết kế

- `schema.sql`: khởi tạo mới đủ 35 bảng.
- `model.json`: danh mục bảng, thuộc tính, khóa và ràng buộc dùng kiểm tra schema.
- `001-add-ThietBiPOS.sql`: nâng cấp 34 → 35 trong giao dịch, không sửa/xóa 34 bảng cũ.
- `../34-bang/`: hồ sơ phiên bản trước, giữ để đối chiếu nâng cấp.

## Bảng ThietBiPOS

| Thuộc tính | Ràng buộc / mục đích |
|---|---|
| MaThietBiPOS | bigint IDENTITY, PK |
| MaMay | varchar(50), NOT NULL, UNIQUE; mã máy Windows chuẩn hóa chữ hoa |
| TenMay | nvarchar(120), NOT NULL, không rỗng |
| MaBamKhoa | binary(32), NOT NULL, UNIQUE; SHA-256 khóa ngẫu nhiên riêng từng máy |
| TrangThai | ACTIVE / REVOKED |
| MaQuanLyCap | FK → TaiKhoanQuanLy.MaQuanLy |
| CapLuc | datetime2, NOT NULL; lần cấp gần nhất |
| ThuHoiLuc | NULL khi ACTIVE; bắt buộc khi REVOKED |
| LyDo | nvarchar(500), NOT NULL, không rỗng |
| ThoiDiemTao, ThoiDiemCapNhat | datetime2, thời điểm lưu |
| PhienBanDuLieu | rowversion chống ghi đè thay đổi đồng thời |

## Vận hành

Chạy `dotnet src/FourChang.Api/bin/Debug/net8.0/FourChang.Api.dll --init-db` từ thư mục gốc sau khi build và Docker sẵn sàng. Bootstrap xác minh schema 34 trước khi nâng cấp; gặp schema lạ sẽ dừng. Sau đó chạy `--check-db`: 35 bảng, 73 FK. Không dùng EnsureDeleted/EnsureCreated trên database đang có dữ liệu.

Khóa thiết bị không thể đọc lại từ server, không ghi trong audit. Máy POS lưu khóa bằng Windows DPAPI theo người dùng hiện tại; `FOURCHANG_POS_KEY` chỉ còn là bí mật ký/xác thực yêu cầu ở máy chủ, không phải khóa đăng nhập chung cho tất cả POS.
