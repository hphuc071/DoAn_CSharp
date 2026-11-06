# 4CHANG — Quy chuẩn giao diện cố định

Phiên bản hiện hành05/11/2026. Áp dụng skill ui-ux-pro-max cho refinement.
Giữ tên **4CHANG LINH NGU LAM**, logo, ảnh và layout của cả ba phân hệ.

## Một palette duy nhất
Không còn lựa chọn Light/Dark, không theo màu hệ điều hành, không đọc/ghi preference màu.
Web dùng tokens.css; Desktop dùng CafeTheme.cs. CafeVisuals chỉ chuẩn hóa control native.

| Token | Giá trị |
|---|---|
| Primary/Accent | #805033 |
| Sidebar espresso | #3A2E25 |
| Background cream | #FBF8F2 |
| Surface | #FFFDF9 |
| Soft | #F4ECE0 |
| Text | #302922 |
| Muted | #716252 |
| Divider | #E1D5C5 |
| Input border | #A28D77 |
| Danger | #A53E33 |

## Layout và component
- Không đổi vị trí sidebar/header/menu/order panel hay thứ tự navigation.
- Font Segoe UI/Tahoma/Arial, nhịp spacing4/8/12/16/24/32px.
- Nút thao tác Desktop44px; input frame48px; header66px. Nút đăng nhập có thể50px theo vai trò.
- Nút + POS44×44, radius22; nút thường radius10. Độ rộng theo caption, không ép bằng nhau.
- Nhóm action gap8px; Field gutter16px; label/filter căn chung baseline.
- Panel/card/table dùng palette ngà–kem, viền nhẹ; selected row rõ và không sửa dữ liệu.
- Tab native theo độ rộng caption; không gán ItemSize trong Resize.
- Sidebar bù scrollbar và ngắt nhãn theo cụm từ khi cửa sổ thấp, không tràn ngang.
- Form/modal giữ tên control, event, binding, owner, xác nhận và các token chống gửi lặp.
- Status có chữ và màu; phân biệt loading/empty/error; không vẽ KPI hoặc chuỗi thời gian giả.

## Chuyển động
Web hover/focus180ms, vào trang220ms, dialog180ms, ưu tiên opacity/transform.
prefers-reduced-motion tắt hiệu ứng. Desktop hover160ms; click không dịch text/icon.
Timer chỉ dùng khi hover/loading đang thay đổi; không có animation trang trí liên tục.

## Bảo toàn
Không thay database, API, nghiệp vụ, quyền, tính tiền hoặc dữ liệu sản phẩm.
Nháp POS vẫn nằm trong Form của phiên, không lưu thông tin cá nhân xuống đĩa.
Cấu hình màu cũ bị bỏ qua nhưng không xóa các cài đặt người dùng.

## QA và báo cáo
[Báo cáo refinement](../../docs/UI-REFINEMENT-REPORT.md) chứa filelist, màu cũ/mới,
ảnh từng màn hình và kết quả kiểm thử. Các báo cáo redesign trước05/11 là lịch sử;
phần Light/Dark trong chúng không áp dụng cho bản hiện hành.
