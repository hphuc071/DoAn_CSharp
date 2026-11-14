# Kết quả chuyển schema30 — 14/11/2026

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
