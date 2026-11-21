# Tinh gọn thuộc tính cho quán nhỏ

Áp dụng trong code ngày 21/11/2026: 30 bảng, 402 thuộc tính, 63 FK, 11 trigger. Bản schema30 trước thay đổi có 423 thuộc tính và 64 FK.

| Bảng | Thuộc tính bỏ |
| --- | --- |
| PhienDangNhap | HoatDongLanCuoiLuc, LoaiUngDung, ThongTinThietBi |
| NguyenLieu | TonAnToan |
| ChiTietCongThuc | PhienBanDuLieu, ThoiDiemCapNhat, ThoiDiemTao, TyLeHaoHut |
| Ban | SoChoNgoi, TenKhuVuc |
| DonHang | MaDonLienQuan |
| MonThemChiTietDonHang | PhienBanDuLieu, ThoiDiemCapNhat, ThoiDiemTao |
| HoaDon | MaSoThueNguoiMua |
| PhieuKho | MocKiemKe |
| ChiTietPhieuKho | PhienBanTonKhoDaChot, SoLuongThucDem, TonSoDaChot |
| BienDongKho | ThoiDiemTao |
| NhatKyHeThong | ThoiDiemTao |

Giữ trường bảo mật/khóa tạm, chống gửi trùng, snapshot, thời điểm nghiệp vụ, audit và rowversion của bản ghi có thể sửa. Các dòng công thức dùng phiên bản và khóa giao dịch của công thức cha; dấu vân tay ảnh hưởng nguyên liệu bao gồm lượng/loại định mức thực tế. Topping đơn giữ snapshot trong đơn cha.

Migration 012 lưu giá trị các cột bỏ vào nhật ký hiện hữu, không tạo bảng phụ. Giá trị được lưu trước khi DROP COLUMN trong cùng transaction; nếu lỗi thì cả nhật ký và DDL rollback. Backup native đã verify được tạo trước transaction nâng cấp. Không xóa khách/đơn/kho hoặc volume.

Tất cả thuộc tính còn lại được liệt kê trong [TU-DIEN-DU-LIEU.md](TU-DIEN-DU-LIEU.md); manifest máy đọc là model.json. Bản schema/model cũ ở ../migrations/30-bang chỉ phục vụ nâng cấp và kiểm thử.

## Tương thích giao diện/API

- Các giao diện hiện hành đã bỏ nhập tồn an toàn/hao hụt/kiểm kê kiểu cũ. DTO cũ giữ chữ ký để client và receipt lịch sử vẫn đọc được; trả 0 hoặc null cho thuộc tính đã bỏ, không ánh xạ ngược xuống SQL.
- Tiền thu/hoàn, giữ chỗ, giá/định mức đã chốt, lịch phiếu kho và banner vẫn giữ chức năng hiện hành.
- Theo yêu cầu tiếp theo ngày21/11/2026, database thật `FourChangDb` đã nâng cấp từ bản35bảng/452cột cũ tới30bảng/402cột. Đã backup/verify, chép backup ra máy và thử phục hồi/nâng cấp riêng trước khi áp dụng thật. Chi tiết ở `KET-QUA-KIEM-THU.md`.

## Nghiệm thu

Build solution và hai harness độc lập không cảnh báo/lỗi. Bộ kiểm thử đầy đủ `artifacts/test-results/columns-full-suite.trx`: **734/734 đạt, 0 lỗi, 0 bỏ qua trong một lượt32m59s**, gồm4 test mới kiểm EF/manifest, lưu giá trị trước tinh gọn, chạy lại, rollback và từ chối schema dở dang. Các kiểm thử dùng database GUID riêng.
