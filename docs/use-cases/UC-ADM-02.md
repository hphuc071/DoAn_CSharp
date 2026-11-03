# UC-ADM-02 — Đối chiếu triển khai

Nguồn nghiệp vụ là đặc tả 26 UC đã chốt; bổ sung duy nhất bảng ThietBiPOS đã được người dùng đồng ý. Không thay đổi danh sách 26 UC.

| Luồng | Mã triển khai | Kiểm chứng |
|---|---|---|
| Chính: đăng nhập, tài khoản hoạt động, phiên và quyền | API/Features/Access/AdminAccountService; Security/SessionAuthentication | API login/lockout, Customer/Staff bị chặn Admin |
| A1: mật khẩu tạm và đăng xuất độc lập ca POS | Desktop/Shell/EntryForm; AdminPasswordForm; AdminAccountService | Phiên PASSWORD_CHANGE bị chặn Dashboard; logout/password giữ nguyên ca |
| A2: đổi mật khẩu duy nhất Admin | AdminPasswordForm; AdminAccountService.ChangePassword | Mật khẩu cũ, chính sách, xác nhận, rowversion; thu hồi mọi phiên Admin |
| A3: cấp/thu hồi thiết bị; quyền Staff cố định | PosDeviceService; PosAccess; PosDeviceForm; PosDeviceCredentials | Khóa riêng, DPAPI, thu hồi chặn ngay; không có tài khoản Staff |
| E3: ca máy thu hồi | AccessPage.ShowShifts; PosDeviceService.CloseRelated | Giữ nguyên chủ ca; còn đơn thì chặn chốt; lưu tiền và người xác nhận |
| E4: phiên bản thay đổi | rowversion trong DTO và service | Version cũ bị từ chối, không ghi đè dữ liệu mới |
| E5: phiên hết hạn/thiết bị thu hồi | SessionAuthentication/PosAccess | Đối chiếu trạng thái máy và phiên bản cấp trong mỗi yêu cầu POS |
| E6: audit/giao dịch, mất phản hồi | Mã thao tác + HMAC + transaction + endpoint biên nhận | Gửi lặp không ghi lần hai; audit lỗi rollback; token đổi mật khẩu cũ chỉ đọc receipt nguồn có thời hạn |

BR liên quan: BR-05, BR-06, BR-07, BR-08, BR-09, BR-14, BR-29. Xem `tests/FourChang.Tests/QuanLy/UC-ADM-02/TestCases.md` để đối chiếu các nhóm kiểm thử.
