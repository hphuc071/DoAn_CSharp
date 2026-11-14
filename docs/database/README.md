# Database hiện hành: 30 bảng

Thiết kế và mã nguồn hiện dùng [schema 30 bảng](30-bang/README.md), 64 khóa ngoại và 11 trigger. `sysdiagrams` của SSMS được giữ riêng, không tính là bảng nghiệp vụ.

- [Schema tạo mới](30-bang/schema.sql)
- [Manifest](30-bang/model.json)
- [Migration từ37](30-bang/008-simplify-to-30.sql)
- [Từ điển dữ liệu](30-bang/TU-DIEN-DU-LIEU.md)
- [Hướng dẫn chạy](../../HUONG-DAN-DATABASE.md)

Các thư mục 34/35/37 giữ lại cho nâng cấp và đối chiếu lịch sử. Không chạy schema cũ để khởi tạo môi trường hiện hành.