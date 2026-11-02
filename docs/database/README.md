# Database hiện tại: 35 bảng

Giữ nguyên 34 bảng đã chốt, bổ sung duy nhất **ThietBiPOS** cho UC-ADM-02 theo xác nhận của người dùng. `dbo.sysdiagrams` của SSMS được giữ nguyên và không tính vào 35 bảng nghiệp vụ.

- [Thiết kế và nâng cấp hiện hành](35-bang/README.md)
- [Schema khởi tạo 35 bảng](35-bang/schema.sql)
- [Manifest mô hình 35 bảng](35-bang/model.json)
- [Migration chỉ thêm thiết bị POS](35-bang/001-add-ThietBiPOS.sql)
- [Từ điển 34 bảng gốc, không thay đổi](34-bang/TU-DIEN-DU-LIEU.md)
- [Hướng dẫn khởi tạo, chạy và kiểm tra](../../HUONG-DAN-DATABASE.md)

Các phiên bản 34 bảng, schema 42 bảng và thiết kế cũ được giữ để đối chiếu. Không dùng schema cũ để khởi tạo database hiện hành. Runner `tests/TichHop/Test-Upgrade35.ps1` dựng schema 34 thật và kiểm tra nâng cấp, bảo toàn sơ đồ SSMS trên database tạm.
