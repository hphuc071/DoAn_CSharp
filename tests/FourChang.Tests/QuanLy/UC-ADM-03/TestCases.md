# Kiểm thử UC-ADM-03 — Quản lý nguyên liệu tinh gọn

BR liên quan: BR-03, BR-08, BR-23, BR-28, BR-29. Tái sử dụng IngredientAdministrationTests.cs và worker AdminFeaturesUiTests; không tạo project test mới.

| Trường hợp | Kết quả yêu cầu |
|---|---|
| Tìm/thêm/sửa hồ sơ g/ml | Giữ tên, mô tả, một ngưỡng và trạng thái; không tạo số dư kho |
| Gửi lại mã thao tác | Cùng payload chỉ ghi một lần; payload khác bị chặn |
| Phiên bản cũ | Thay đổi thực sự bị từ chối; không ghi đè hồ sơ mới |
| Ngưỡng 0, số lẻ, âm, quá 4 số lẻ | 0 và số hợp lệ lưu chính xác; âm/quá precision bị chặn |
| Dữ liệu tồn an toàn cũ | Không tạo ngưỡng thứ hai; hạ cảnh báo về0 được, không đổi số dư |
| Đơn vị ngoài g/ml, quy đổi tự khai báo | Từ chối; không tạo đơn vị/quy đổi |
| Đổi đơn vị đã tham chiếu | Chặn cả UI và API | Ngừng thiếu xác nhận hoặc dấu ảnh hưởng cũ | Từ chối, nguyên liệu vẫn đang dùng |
| Ngừng sau xác nhận mới | Món phụ thuộc không khả dụng; BOM và kho giữ nguyên |
| Kích hoạt lại | Khôi phục khả dụng nếu đủ điều kiện, không nhập thêm kho |
| Xóa từ client cũ | 409; giữ hồ sơ và lịch sử |
| Không ghi được audit | Rollback hồ sơ và sự kiện, không lưu dở dang |
| WinForms | Chỉ4cột,2nút, không tồn an toàn/quy đổi; thêm cả g/ml, giữ12,5; xác nhận ngừng có danh sách ảnh hưởng; footer không bị cắt |

Chạy cuối: adm03-simplified-final.trx8/8; adm03-ui-final.trx1/1 (UI được chạy lại sau xử lý lỗi preflight). Lượt adm03-simplified.trx ban đầu7/8: fixture sửa riêng trường tồn an toàn đã bị bỏ nên là no-op, không đổi rowversion; đã chỉnh fixture sửa ngưỡng thật và chạy lại toàn bộ8test đạt.

Fixture SQL GUID riêng, không xóa/reset database đồ án. Đã kiểm trực quan danh sách, form và hộp xác nhận. Kiểm mất mạng: chưa rõ kết quả phải giữ nháp, tra receipt/thử lại cùng mã, không báo thành công giả.

## Tìm kiếm không nhảy trang — 18/11/2026
WinForms nhập từng ký tự Sữa tươi, thêm khoảng trắng, lọc không có kết quả và xóa bộ lọc. Kiểm nguồn bảng không thay, kết quả giống không reset binding, khung bảng/ô nhập không đổi vị trí và focus không mất. ingredient-search-caret-final.trx1/1 đạt; ảnh ADM03-search-stable.png. Những lần kiểm trước phát hiện cuộn native24px đã được sửa; không tính các lượt lỗi là đạt.
