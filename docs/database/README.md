# Database hiện tại: 37 bảng nghiệp vụ

Giữ 35 bảng trước đó; bổ sung `Banner`, `SuKienDongBo` và ba cột mặc định/lịch công bố theo phê duyệt ADM-03. Không xóa dữ liệu hay thay khóa hiện có; 73 FK giữ nguyên. `dbo.sysdiagrams` được giữ và không tính vào số bảng nghiệp vụ.

- [Thiết kế/nâng cấp hiện hành](37-bang/README.md)
- [Migration 35 → 37](37-bang/003-menu-and-outbox.sql)
- [Manifest 37 bảng](37-bang/model.json)
- [Schema đầy đủ](37-bang/schema.sql)
- [Phiên bản 35 trước nâng cấp](35-bang/README.md)

Bootstrap nhận diện đúng phiên bản 34/35/37; chỉ nâng cấp từ schema được xác minh. Schema lạ bị chặn. Không chạy EnsureDeleted/EnsureCreated hoặc dán schema khởi tạo lên database có dữ liệu.
