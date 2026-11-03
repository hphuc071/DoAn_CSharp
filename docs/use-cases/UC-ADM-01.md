# UC-ADM-01 Quản lý tài khoản khách hàng

Nguồn phạm vi: đặc tả 26 UC, UC-ADM-01; BR-06, BR-07, BR-08, BR-09, BR-14, BR-28, BR-29. Chức năng này không tạo tài khoản nhân viên, không sửa đơn/hóa đơn/kho/hoàn tiền và không thay đổi 35 bảng đã chốt.

## Cấu trúc mã

| Vị trí | Trách nhiệm |
| --- | --- |
| `src/Backend/FourChang.Api/Features/Customers/CustomerEndpoints.cs` | Route và quyền Admin cho tìm, xem, tạo, sửa, trạng thái, reset, xóa, tra kết quả |
| `CustomerAccountService.cs` | Chuẩn hóa/duy nhất, rowversion, kiểm tra tham chiếu, giao dịch, thu hồi phiên, audit và chống trùng |
| `CustomerRules.cs` | Kiểm tra dữ liệu và DTO không chứa bí mật |
| `CustomerTemporaryPasswordEndpoints.cs` | Thực thi điều kiện buộc đổi mật khẩu tạm trước khi dùng tài khoản |
| `src/Shared/FourChang.Presentation.Shared/Integration/Customers/` | Hợp đồng dữ liệu và HTTP client dùng chung |
| `src/Presentation/FourChang.Desktop/Features/Admin/Customers/` | Danh sách, phân trang, hồ sơ/tham chiếu và biểu mẫu thao tác |
| `src/Presentation/FourChang.Web/Pages/Account/ChangeTemporaryPassword.*` | Trang đổi mật khẩu tạm trước khi khách được dùng phiên đầy đủ |

## Đối chiếu đặc tả

| Luồng | Thực hiện |
| --- | --- |
| Chính 1 | Tìm theo mã/tên/tên đăng nhập/email/điện thoại, phân trang ổn định |
| Chính 2–3 | Tạo/mở hồ sơ; định danh chuẩn hóa và duy nhất; tạo bằng mật khẩu tạm băm, bắt buộc đổi |
| Chính 4–5, A1 | Sửa, khóa/mở/ngừng và reset; trạng thái/reset có lý do; rowversion; khóa/reset thu hồi phiên; audit trước/sau |
| A2, E2 | Kiểm tra mọi FK; chỉ xóa khi chưa tham chiếu, có xác nhận và audit; có lịch sử thì giữ và dùng khóa/ngừng |
| E1 | Dữ liệu sai bị từ chối; unique SQL chặn cạnh tranh |
| E3 | Trả 409 và hồ sơ hiện hành khi version cũ; form cho đối chiếu bản mới, giữ nháp |
| E4 | Mất mạng giữ nháp; chưa rõ kết quả thì tra mã thao tác, thử lại cùng nội dung, không tự báo thành công |
| E5 | Tất cả API quản trị yêu cầu Admin; khách/POS bị từ chối |
| E6 | Hồ sơ, thu hồi phiên và audit cùng transaction; lỗi rollback |

## Quyết định thực thi từ ràng buộc sẵn có

- Dùng `BatBuocDoiMatKhau` và `PhienDangNhap.PhamViPhien=PASSWORD_CHANGE` sẵn có. Phiên hạn chế chỉ đổi mật khẩu/đăng xuất; không đọc hồ sơ, đơn hoặc gọi Admin. Đổi thành công thu hồi phiên, yêu cầu đăng nhập lại.
- Dùng `NhatKyHeThong.MaThaoTac`, `MaBamYeuCau`, `KetQuaDaChotJson` sẵn có để trả lại kết quả. Yêu cầu có mật khẩu được HMAC bằng bí mật máy chủ; mật khẩu và hash mật khẩu không nằm trong audit/DTO/log.
- Xóa khách chỉ được cấp DELETE ở `TaiKhoanKhachHang`; 34 bảng khác vẫn bị DENY DELETE. Không xóa lịch sử hoặc tự xóa địa chỉ/phiên để vượt FK. Tài khoản có bất kỳ tham chiếu sẽ chuyển sang khóa/ngừng theo lựa chọn của Admin.
- Mật khẩu tạm chỉ ở biểu mẫu đang mở để bàn giao; không lưu bản rõ vào CSDL/tệp. Tên đăng nhập hiện hữu chỉ đọc; cập nhật hồ sơ gồm họ tên, email và điện thoại như các trường nghiệp vụ đã có.

## Chạy và kiểm thử

Chạy `ChuanBi-Database.ps1` một lần sau cập nhật để áp dụng quyền DELETE có giới hạn, sau đó mở ứng dụng Quản lý và chọn Khách hàng. Backend và Web phải được khởi động bằng bản build mới để kiểm tra việc buộc đổi mật khẩu.

`tests/FourChang.Tests/QuanLy/UC-ADM-01/CustomerAdministrationTests.cs` kiểm thử xUnit trên database GUID riêng gồm 35 bảng nghiệp vụ; không thao tác dữ liệu khách thật. Xem `tests/README.md` về cấu hình và dọn dữ liệu.

## Bảng sơ đồ SSMS

Theo xác nhận của người dùng, giữ nguyên `dbo.sysdiagrams` sẵn có. Bộ kiểm tra chỉ bỏ qua bảng này khi có dấu `microsoft_database_tools_support=1`; PK của nó không được tính vào 35 PK nghiệp vụ. Những bảng lạ khác vẫn làm kiểm tra thất bại. Không thêm/xóa bảng hoặc cột nghiệp vụ để xử lý trường hợp này.

## Kết quả kiểm tra

- Build solution: 0 cảnh báo, 0 lỗi.
- UC-ADM-01: 67 kiểm tra HTTP/SQL trên database tạm, gồm cạnh tranh, phân trang, quyền, reset/thu hồi phiên, xóa có điều kiện và rollback khi audit lỗi.
- WinForms: 16 kiểm tra và 12 ảnh; gồm tạo/sửa/xóa thực tế, giữ nháp và tra kết quả khi cắt phản hồi sau commit.
- Web: 8 kiểm tra luồng đổi mật khẩu tạm, xác nhận mật khẩu, không trả mật khẩu về HTML và antiforgery.
- Hồi quy API cũ: 25 kiểm tra; ràng buộc SQL: 17 nhóm kiểm tra rollback.
- Dữ liệu kiểm thử nằm trong database tạm riêng và được dọn sau kiểm tra.

## File test trong repository

Bảng tình huống: [tests/FourChang.Tests/QuanLy/UC-ADM-01/TestCases.md](../../tests/FourChang.Tests/QuanLy/UC-ADM-01/TestCases.md). Chạy chung: `dotnet test FourChang.sln --filter "UseCase=UC-ADM-01"`. Các file test giao diện đã được đưa vào thư mục UC, không phụ thuộc đường dẫn máy của người viết.

## Cải thiện màn hình Khách hàng

Giữ nguyên chức năng UC-ADM-01, làm sáng tông nâu–kem, thêm hiệu ứng rê chuột/nhấn nút, nhãn trạng thái và tiến trình tải nhẹ. Nút Tải lại đọc từ khóa hiện có trong ô tìm kiếm và đưa về trang 1; xóa từ khóa sẽ lấy lại toàn bộ danh sách và tổng số khách. Test hồi quy nằm tại TC-ADM-01-UI-011 đến UI-016. Các control trang trí riêng đặt trong `Features/Admin/Customers/Controls/CustomerVisuals.cs`; không thay theme toàn ứng dụng.
