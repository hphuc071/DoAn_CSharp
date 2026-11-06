# Thực đơn Admin — UI refinement 06/11/2026

Giữ nguyên thương hiệu, palette CafeTheme, sidebar/header và năm nhóm ADM-03. Không thay API/schema/logic giá/BOM hoặc handler nghiệp vụ.

- Điều hướng dạng nút có focus/hover, selected màu nâu; TabControl native giữ SelectedIndex và TabPages, không hiện khung 3D.
- Một thẻ danh sách sáng; tiêu đề và số dòng thật, tìm kiếm/action cùng baseline 44px.
- Toolbar chuyển hai hàng nếu thiếu chỗ. Chỉ bảng cuộn dọc; chia cột theo chiều rộng còn lại sau scrollbar để không cắt cột cuối.
- Badge có chữ/màu cho công bố, bán, sử dụng, mặc định; không sửa giá trị ô.
- Form có tiêu đề theo đối tượng, trường liên quan chia hai cột, mô tả dài nhiều dòng, footer Hủy/Lưu cố định.
- Animation dùng CafeButton/AdminActionButton và AdminLoadingStrip hiện có; không thêm timer nền hoặc theme mới.
