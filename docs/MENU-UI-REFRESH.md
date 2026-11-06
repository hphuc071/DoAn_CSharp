# Tinh chỉnh giao diện Thực đơn — 06/11/2026

Đã thiết kế lại màn hình Thực đơn và năm form thêm/sửa, giữ sidebar/header, thương hiệu 4CHANG LINH NGU LAM và palette nâu–kem. Không sửa backend, API contract, database, giá hay các điều kiện lưu/xóa/công bố.

## File

- src/Presentation/FourChang.Desktop/Admin/Menu/MenuManagementPage.cs — ghép điều hướng/card, theo dõi resize, trình bày giá và độ rộng cột.
- src/Presentation/FourChang.Desktop/Admin/Menu/MenuListLayout.cs — helper giao diện cục bộ: tab content, toolbar, bảng, badge, loading và empty state.
- src/Presentation/FourChang.Desktop/Admin/Menu/MenuEditorForm.cs — tiêu đề theo đối tượng, ghép trường liên quan, mô tả nhiều dòng; giữ nguyên payload, phiên bản và receipt/handler lưu.
- tests/FourChang.Tests/QuanLy/UC-ADM-03/MenuUiTests.cs — bấm tab thật, kiểm tra selected state, chiều cao 44px, không cuộn ngang, tìm/xóa từ khóa, thêm danh mục và POS.

## Kiểm tra

Build solution: 0 cảnh báo, 0 lỗi. Bộ cuối gồm MenuUiTests và RedesignConnectedTests: 2/2 đạt. TRX: artifacts/tests/menu-ui-refresh/menu-refinement-delivery.trx.

Đã xem ảnh từng tab/form, ở cửa sổ 1440×960 và 1100×740. Đã sửa ô tìm cao hơn nút, cột tràn gây cuộn ngang và trạng thái tab đầu chưa được đánh dấu. Các test dùng database GUID riêng, không thay dữ liệu đồ án.

Ảnh: [Desktop](previews/admin-menu-refined.png), [Cửa sổ nhỏ](previews/admin-menu-refined-compact.png).

Mở lại Quản lý → Thực đơn để sử dụng bản mới. Không tạo thêm chức năng nghiệp vụ hoặc chế độ sáng/tối.
