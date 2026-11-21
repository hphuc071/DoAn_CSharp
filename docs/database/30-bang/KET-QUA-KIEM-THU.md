# Kết quả kiểm thử schema30

## Tinh gọn thuộc tính — 21/11/2026

- Code/model/SQL mới: 30 bảng, 402 thuộc tính, 63 FK, 11 trigger; loại 21 cột. Từ điển dữ liệu liệt kê đủ 402 cột.
- Build solution và hai harness CheckoutSql/IntegrationClosure: 0 cảnh báo, 0 lỗi.
- `columns-migration-final.trx`: 10/10 đạt; gồm schema rỗng, nâng 34/37 và schema30 cũ, giữ tiền/kho/snapshot, lưu giá trị cột bỏ, chạy lại, rollback lỗi DDL, từ chối schema dở dang.
- `columns-full-suite.trx`: **734/734 đạt trong một lượt, 0 lỗi, 0 bỏ qua**, thời gian32m59s. Bao gồm API/SQL, Playwright, WinForms và4 test tinh gọn mới; EF ánh xạ đúng30bảng/402cột/63FK, migration chạy lại/rollback/schema dở dang đều đạt với bản cuối.
- Bộ734test dùng database GUID riêng. Theo yêu cầu tiếp theo, đã nâng database thật như mục bên dưới. Chưa commit/push.

## Áp dụng database thật — 21/11/2026

- Nguồn thực tế `FourChangDb` trên SQL14330 là **35bảng/452cột/73FK**, khớp đầy đủ manifest35Legacy; không phải bản423cột đã dùng trong đề xuất. Trước nâng có1khách,6món,0đơn,0thanh toán,0hóa đơn. Không có Web/API/Desktop của repo đang chạy lúc nâng cấp.
- Backup native COPY_ONLY/CHECKSUM + RESTORE VERIFYONLY, chép ra host: `artifacts/backups/columns-live-20261121/FourChangDb_beforeColumns_20261121_bfe9a879d85b4d2593b5fb702cc5c353.bak` (11.653.120bytes). SHA256 và metadata trong `backup-host.json`; snapshot trước chỉ lưu hash/số dòng, không ghi dữ liệu cá nhân hoặc bí mật cấu hình.
- Phục hồi bản backup vào `FourChang_Columns_Rehearsal_20261121_2ec557f6`, chạy chuỗi bootstrap35→37→30→402cột và so dữ liệu thành công trước khi chạy bản thật. Giữ database rehearsal để đối chiếu, không xóa database/volume.
- Bản thật sau nâng: **30bảng/402cột/63FK/11trigger**. Đối chiếu28bảng cũ còn sử dụng: số dòng/hash các cột chung khớp; rowversion của Mon/NguyenLieu/CauHinhMon được loại khỏi so hash vì các migration chuyển đổi cập nhật các bảng này. Kiểm riêng chuyển danh mục→LoaiMon, đơn vị→DonViCoSo và giữ chỗ→snapshot đều khớp. Hai bảng mới Banner/SuKienDongBo không có dữ liệu nguồn cũ để so.
- Giá trị21cột bỏ được so hash với8bản ghi archive tương ứng các bảng có dữ liệu: khớp. Các bảng con rỗng không tạo archive. Các trường đã bỏ từ migration37→30 và7bảng cũ vẫn có trong bản backup35 đầy đủ.
- `--check-db` bằng fourchang_app và DBCC CHECKCONSTRAINTS đều đạt. Bằng chứng `before.json`, `after-rehearsal.json`, `after.json` trong thư mục backup. Không chạy test tạo đơn/khách giả trên database thật.
- API/Web đã khởi động và kiểm đọc: ready30bảng, /api/menu6món; trang chủ HTTP200 có thương hiệu. Cấu hình local hiện hành đặt API http://localhost:5181, Web http://localhost:15880; bằng chứng `app-readonly-check.json`. Cần mở Desktop bằng launcher để nhận DLL mới. Chưa push GitHub.

## Kết quả chuyển schema30 — 14/11/2026 (lịch sử)

Database đang dùng `FourChangDb` đã chuyển thành **30 bảng nghiệp vụ, 64FK, 11trigger**. `sysdiagrams` được giữ nguyên. EF Core ánh xạ30 entity; solution build không lỗi.

## Bảo toàn dữ liệu

- Sao lưu native COPY_ONLY/CHECKSUM, RESTORE VERIFYONLY; phục hồi vào FourChang_Migration30_20261114 trước khi chuyển thật.
- So sánh từng cột giữ lại của30 bảng và toàn bộ giữ chỗ với nguồn37: khớp.
- Sau chuyển thật, so sánh toàn bộ cột dữ liệu (trừ rowversion tái tạo) của30 bảng với bản phục hồi đã thử: khớp. Kiểm tra này thực hiện trước khi khởi động worker.
- `--check-db` và `--test-db` trên database đang dùng đều đạt. Kiểm thử SQL dùng transaction rollback.
- Backup mới nhất trên máy: `artifacts/backups/schema30-20261114/FourChang_before30_final.bak`; metadata/hash trong `backup-final.json`. Backup và dữ liệu local nằm ngoài Git.

## Kiểm thử

**76 trường hợp riêng biệt đạt**, không có test bị bỏ qua trong các lượt dưới:

| Kết quả TRX trong artifacts/test-results | Đạt | Trượt |
| --- | ---: | ---: |
| schema30-final-api.trx | 56 | 0 |
| schema30-ui-migration.trx | 13 | 0 |
| schema30-address-reconcile.trx | 9 | 0 |

Hai test migration xuất hiện trong cả lượt API và lượt UI; tổng riêng biệt đã loại trùng. Bao gồm database rỗng; nâng34 có sơ đồ SSMS; nâng37 có đơn/tiền/hóa đơn/giữ chỗ; lỗi DDL muộn rollback rồi thử lại; chạy lại idempotent; snapshot bất biến; BOM/kho; CASH/QR mô phỏng; hủy chờ duyệt/hoàn quầy; phân ca; hóa đơn/báo cáo; địa chỉ và đối soát. Web/WinForms chạy với API/SQL thật trên database fixture riêng, có ảnh bằng chứng.

Các harness SQL độc lập được đồng bộ để build theo schema30; không chạy thêm container kiểm chứng riêng của các harness này. Không coi lượt hồi quy được chọn là toàn bộ mọi test trong repo.

## Code và giao diện

- Không còn entity/config/DbSet cho7 bảng đã bỏ; không tạo view tương thích giả bảng.
- API định giá, BOM, đặt món, pha chế, hủy/giải phóng, đối soát và báo cáo cùng dùng bảng nguyên liệu đơn đã gộp.
- Desktop không nạp khóa thiết bị cũ; tab thiết bị/danh mục CRUD và editor cặp đường đá/topping riêng đã bỏ. Loại món, g/ml và mức pha chế dùng quy tắc chung.
- Bỏ tọa độ/khoảng cách khỏi form đổi địa chỉ giao; vẫn cần chấp thuận và chứng cứ, giữ phí đã chốt.
- Sửa metadata mật khẩu Web còn10 thành5 đúng quy tắc đã chốt.
- Cần đóng/mở lại Admin/POS để dùng DLL mới. Không sửa mật khẩu quản lý hoặc dữ liệu giao dịch hiện hữu.

## Vận hành checkout sau nâng — 21/11/2026

- Đã bật cấu hình local bằng scripts/Enable-LocalDemo.ps1 trên .env hiện hành: tạo đơn Web/API, Development, QR mô phỏng (link đúng API5181), giao hàng15.000đ/chọn bàn và yêu cầu hủy có duyệt. Giữ mặc định an toàn trong .env.example; README chỉ cách opt-in cho local. Không mở thanh toán ngân hàng thật.
- Sửa checkout không hiển thị yêu cầu đăng nhập sai khi tạo đơn đang tắt; giữ giỏ và mã thao tác đang chờ. Khi writer bật, vẫn yêu cầu tài khoản khách đã xác minh và kiểm giá/chống trùng như cũ.
- Build test sạch. operational-checkout-flow.trx: **79/79đạt,0lỗi/0skip,4m30s**, gồm2case mới cho thông báo khi writer tắt và các luồng đặt/QR/hủy/xử lý đơn. Test dùng database riêng; cấu hình fixture tách khỏi .env thật.
- API/Web thật đã chạy lại: ready30bảng, CheckoutEnabled=true, FulfillmentEnabled=true, HTTP200. Bằng chứng artifacts/column-simplification/operational-live-check.json; ảnh checkout-delivery-390.png ở fixture Simulated-Fulfillment/37e3ff2fc0674bd39374fc784471c78e đã được xem. Không tạo đơn/khách giả trên FourChangDb để kiểm thử. Chưa push.
