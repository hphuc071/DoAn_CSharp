# Kết quả kiểm thử nhóm tài khoản và quyền truy cập

Ngày kiểm tra: 02/11/2026. Phạm vi: WEB-01/02/03 liên kết ADM-01/02 và quyền POS-01. Giữ nguyên 26 UC và 35 bảng nghiệp vụ; không thay schema hoặc dữ liệu kinh doanh thật.

| Bộ kiểm thử | Số kiểm tra đạt | Nội dung chính |
|---|---:|---|
| UC-WEB-01 API | 17 | Đăng ký, xác nhận mật khẩu, định danh trùng, receipt, không tự login, audit rollback và quyền Customer |
| UC-WEB-02 API | 17 | Phiên, login/logout gửi lặp, hết hạn, bộ đếm sai, khóa tạm và audit rollback |
| UC-WEB-03 API | 30 | Hồ sơ, sổ địa chỉ, default duy nhất, sở hữu, rowversion, snapshot đơn cũ, đổi mật khẩu và thu hồi nhiều phiên |
| Web AccountAccess | 24 | Form thật, CSRF, mật khẩu không trả ngược trong HTML, mất phản hồi sau commit, không ghi lần hai, logout offline và phục hồi mật khẩu tạm |
| AccountAccess liên phân hệ | 25 | Web/Admin cùng hồ sơ; khóa/mở/reset; phiên hạn chế; receipt mật khẩu tạm; Staff/Customer không vào Admin; thu hồi máy giữ ca |
| API tích hợp có sẵn | 25 | Hồi quy Admin, Customer, POS và phiên, không tạo thanh toán hay biến động kho |
| UC-ADM-01 | 67 API và 16 WinForms và 8 Web | Hồi quy quản lý khách, form thao tác, tìm kiếm/tải lại, mật khẩu tạm |
| UC-ADM-02 | 64 API và 16 WinForms | Hồi quy Quản lý duy nhất, quyền, thiết bị, ca liên quan và DPAPI |

Build solution thành công, không có cảnh báo hoặc lỗi. Mỗi bộ chạy với database tạm riêng; runner dừng đúng tiến trình và xóa đúng database do nó tạo trong finally. Ảnh giao diện và result.json được giữ dưới artifacts/tests theo actor, UC và run-id, bị loại khỏi Git. Không đưa mật khẩu, token hoặc .env vào báo cáo.

Các phép thử dùng chung có thể đi qua cùng nghiệp vụ; số lượng trong bảng là assertion của từng bộ, không phải số UC mới. Test POS-01 ở nhóm này chỉ xác minh phần quyền liên quan ADM-02; không đánh dấu hoàn thành toàn bộ UC-POS-01.

Hướng dẫn chạy và vị trí file: docs/use-cases/ACCOUNT-ACCESS.md; bảng tình huống riêng: tests/KhachHang/UC-WEB-01 đến UC-WEB-03/TestCases.md; tích hợp: tests/TichHop/AccountAccess/README.md; quyền POS: tests/NhanVien/UC-POS-01/TestCases-Access.md.
