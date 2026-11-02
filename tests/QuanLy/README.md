# Kiểm thử actor QuanLy

Mỗi UC có thư mục riêng theo mã, ví dụ `ADM` nằm trong `QuanLy/UC-ADM-xx/`. Chỉ thêm test cho đúng UC đã chốt; không tạo UC mới từ tình huống kiểm thử.

| Mã UC | Tên UC | Trạng thái |
| --- | --- | --- |
| UC-ADM-01 | Quản lý tài khoản khách hàng | [Có bảng test case và script](UC-ADM-01/TestCases.md) |
| UC-ADM-02 | Quản lý quyền truy cập hệ thống | [64 kiểm tra API và 16 WinForms](UC-ADM-02/TestCases.md) |
| UC-ADM-03 | Quản lý thực đơn | Chưa có bộ test riêng |
| UC-ADM-04 | Quản lý nguyên liệu | Chưa có bộ test riêng |
| UC-ADM-05 | Quản lý công thức pha chế BOM | Chưa có bộ test riêng |
| UC-ADM-06 | Theo dõi tồn kho và cảnh báo | Chưa có bộ test riêng |
| UC-ADM-07 | Quản lý chứng từ kho | Chưa có bộ test riêng |
| UC-ADM-08 | Giám sát và xử lý đơn hàng | Chưa có bộ test riêng |
| UC-ADM-09 | Tra cứu và quản lý hóa đơn | Chưa có bộ test riêng |
| UC-ADM-10 | Xử lý và xác nhận hoàn tiền | Chưa có bộ test riêng |
| UC-ADM-11 | Thống kê, báo cáo và tra cứu nhật ký hệ thống | Chưa có bộ test riêng |
| UC-ADM-12 | Quản lý danh sách nhân viên | Chưa có bộ test riêng |
| UC-ADM-13 | Quản lý ca làm việc | Chưa có bộ test riêng |


UC-ADM-02 đã có bộ test tại `QuanLy/UC-ADM-02/` (API, WinForms, đối chiếu BR). Database hiện hành: **35 bảng nghiệp vụ** sau khi bổ sung `ThietBiPOS` theo xác nhận; manifest vẫn giữ đúng **26 UC**. Xem hướng dẫn tại `QuanLy/UC-ADM-02/README.md`.
