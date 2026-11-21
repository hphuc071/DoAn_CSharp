# Database hiện hành: 30 bảng

Thiết kế và mã nguồn hiện dùng [schema 30 bảng](30-bang/README.md), 64 khóa ngoại và 11 trigger. `sysdiagrams` của SSMS được giữ riêng, không tính là bảng nghiệp vụ.

- [Schema tạo mới](30-bang/schema.sql)
- [Manifest](30-bang/model.json)
- [Migration từ37](30-bang/008-simplify-to-30.sql)
- [Từ điển dữ liệu](30-bang/TU-DIEN-DU-LIEU.md)
- [Hướng dẫn chạy](../../HUONG-DAN-DATABASE.md)

Thư mục [migrations](migrations) giữ các script và manifest 34/35/37 còn được bootstrap và test nâng cấp sử dụng. Các tài liệu mô tả schema cũ và bản thiết kế nháp đã được dọn. Khi tạo mới dùng schema 30 bảng.
