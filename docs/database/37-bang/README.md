# Schema hiện hành — 37 bảng, 74 khóa ngoại

Đã được người dùng duyệt: thêm đúng hai bảng `Banner`, `SuKienDongBo` và ba cột `Mon.CongBoTuLuc`, `CauHinhMon.LaMacDinh`, `TuyChonPhaCheHopLe.LaMacDinh`. Không thêm bảng giá theo kênh.

## Nâng cấp

1. Sao lưu database và kiểm tra đọc được bản sao.
2. Build `FourChang.sln`.
3. Từ gốc repo, chạy `dotnet src/Backend/FourChang.Api/bin/Debug/net8.0/FourChang.Api.dll --init-db`.
4. Chạy cùng DLL với `--check-db`, rồi `--test-db`.

Bootstrap xác minh schema cũ trước khi chạy migration trong transaction. Dữ liệu cũ có lịch NULL, chọn mặc định từ cấu hình hiện có (ưu tiên M/lạnh, cặp 50/50 nếu có). Mã/giá/đơn cũ không bị đổi. Database mới cũng khởi tạo bằng cùng chuỗi nâng cấp. Chạy lại không tạo thêm bảng hoặc dữ liệu trùng.

## Hai bảng mới

| Bảng | Thuộc tính |
|---|---|
| Banner | MaBanner (PK IDENTITY), TenBanner, NoiDung, DuongDanAnh, LienKet, ViTri, ThuTuHienThi, BatDauLuc, KetThucLuc, DaCongBo, ThoiDiemTao, ThoiDiemCapNhat, PhienBanDuLieu |
| SuKienDongBo | MaSuKien (PK GUID), MaThaoTac (UNIQUE), LoaiSuKien, NoiDungJson, PhatSinhLuc, XuLyXongLuc, SoLanThu, ThuLaiLuc, KhoaDenLuc, MaLanXuLy, LoiGanNhat, PhienBanDuLieu |

Kiểu/NULL/CHECK chính xác tại [migration](003-menu-and-outbox.sql), ánh xạ tại `Configurations/Menu`. Banner chỉ dùng vùng HOME_HERO đang có. Thời gian UTC, kết thúc lớn hơn bắt đầu. JSON sự kiện phải hợp lệ, số lần thử không âm, lease/token cùng NULL hoặc cùng có giá trị. Filtered UNIQUE giữ tối đa một mặc định mỗi món/cấu hình; API kiểm tra đúng một khi công bố.

## Quyền SQL

App chỉ được DELETE các bảng danh mục/món/cấu hình/topping/liên kết/tùy chọn/banner mà ADM-02 cần. API vẫn kiểm tra Admin, version, tham chiếu trong transaction. Đơn/tiền/audit/outbox tiếp tục bị DENY DELETE. ADM-06 cho DELETE phiếu kho và dòng phiếu để xóa nháp; trigger và service vẫn chặn xóa/sửa phiếu POSTED. Không cấp quyền thay schema cho tài khoản ứng dụng.

## Outbox/change feed

Menu, audit/receipt và sự kiện commit cùng transaction. Worker lease tối đa 10 hàng, giữ token và mốc hết hạn; phát thành công bằng cách công bố vào feed SQL (`XuLyXongLuc`). SSE đọc feed SQL nên nhiều instance vẫn nhìn cùng thay đổi. Phát lỗi thử lại sau 5 giây nếu ghi được trạng thái; mất kết nối hoàn toàn thì lease 30 giây hết hạn cho lần nhận sau. Client reconnect luôn tải lại snapshot từ API. Đây là thông báo tải lại, không phải cam kết mọi client đã nhận ngay.

Worker đặt READ COMMITTED rõ ràng trước READPAST vì connection pool có thể giữ mức cô lập của lệnh Admin trước đó. Theo dõi lịch công bố theo giờ máy chủ ngay cả khi không có lệnh ghi mới. Không xóa lịch sử sự kiện trong tính năng này.

## Bổ sung ADM-01..05 theo đặc tả 25 UC

`004-customer-phone-rules.sql` bổ sung hai CHECK điện thoại 10 chữ số ASCII, không thêm bảng. Script dừng nếu dữ liệu cũ sai, không tự cắt số. EF mapping và manifest kiểm tra cùng ràng buộc.

ADM-03/04 được cấp DELETE có giới hạn trên NguyenLieu, DonViTinh, QuyDoiDonViNguyenLieu, CongThuc và ChiTietCongThuc; API vẫn chặn bản đã công bố/tham chiếu. Không cấp DELETE trên số dư, giữ chỗ, biến động, đơn/tiền/audit/outbox. Nâng cấp chạy bằng tài khoản quản trị; API nghiệp vụ dùng fourchang_app.

## Điện thoại bắt buộc

`005-required-customer-phone.sql` nâng cột TaiKhoanKhachHang.SoDienThoaiChuanHoa thành NOT NULL, giữ CHECK 10 chữ số ASCII và unique index hiện có. Không thêm bảng. `model-before-required-phone.json` phục vụ nhận diện schema cũ trước nâng cấp; `model.json` mô tả schema hiện tại. Trước ALTER, script kiểm tra dữ liệu rồi thực hiện trong transaction; không cắt số, tự sinh số hay xóa khách để vượt kiểm tra.

## Bổ sung ADM-06 đến ADM-10

Migration `006-admin-operations-and-finance.sql` đã được chấp thuận: thêm `DonHang.MaXuLyNgoaiLe`, `HoSoHoanTien.LoaiNguon/ChungCuYeuCau`, `HoaDon.MaHoanTien` cùng FK và unique nguồn điều chỉnh. EXPIRED tách hết hạn khỏi hủy chủ động. Giữ 37 bảng, tổng FK là 74. Nguồn hoàn cũ là LEGACY; không tự đoán hoặc xóa dữ liệu.

Xem [hướng dẫn tích hợp ADM-06..10](../../INTEGRATION-ADM-06-10.md).
## Mở nhiều ca độc lập

`007-independent-work-sessions.sql` bỏ unique trên hai index ca chưa kết thúc theo nhân viên/máy; thay bằng index thường có filter giữ hiệu năng tra cứu. `--init-db` áp dụng idempotent, không thay bảng/cột/FK hay đóng/xóa ca cũ. EF và schema/model hiện tại đã đồng bộ. Xem [quy tắc ca](../../SHIFT-DAILY-RULE.md).
