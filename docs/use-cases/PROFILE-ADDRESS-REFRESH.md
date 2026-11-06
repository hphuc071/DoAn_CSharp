# Hồ sơ và địa chỉ được điều chỉnh theo xác nhận

Chủ dự án đã chọn thêm DiaChiLienHe vào NhanVien và cho Admin thêm/sửa/ngừng/chọn mặc định địa chỉ giao hàng khách. Vẫn 35 bảng, 73 FK và 26 UC. Không sinh tài khoản Staff hoặc sửa địa chỉ snapshot của đơn cũ.

## Database và tích hợp nhóm

- NhanVien thêm DiaChiLienHe nvarchar(500) NULL, dữ liệu cũ giữ nguyên. Có bản nhận diện model 35 cũ và patch 002-employee-contact-address.sql.
- Chạy `dotnet run --project src/Backend/FourChang.Api -- --init-db` để nâng cấp, rồi `--check-db`. Bootstrap chỉ nâng cấp model cũ đã khớp, giữ sysdiagrams và mã dữ liệu. Database 34 cũ vẫn nâng cấp qua thiết bị POS rồi bổ sung cột.
- Contracts Employee/EmployeeInput thêm trường DiaChiLienHe cuối record. Admin xem/lưu; các endpoint POS không trả địa chỉ nhân viên.
- Admin dùng `/api/admin/customers/{owner}/addresses`, POST thêm, PUT `/{id}` sửa, POST `/{id}/default` hoặc `/{id}/deactivate`. GET `/api/admin/customers/address-operations/{guid}` đọc receipt.
- Payload lưu dùng SaveSelfAddress đã có: MaThaoTac, PhienBanHoSo, PhienBanDiaChi và thông tin nhận hàng. Thao tác mặc định/ngừng dùng AdminAddressAction có GUID và hai version. Nhận contract/client cùng API khi merge.
- Web và Admin dùng chung DiaChiKhachHang, cùng version hồ sơ; cạnh tranh trả 409. Chỉ Admin được gọi endpoint Admin, Customer không tự chỉ định owner khác.
- Ngừng dùng giữ bản ghi, Web chỉ hiện địa chỉ còn dùng. Mặc định tối đa một địa chỉ đang dùng nhờ unique index. Không sửa đơn đã gửi. Khi sửa text địa chỉ trong UI, tọa độ cũ được bỏ để xác định lại khi đặt giao hàng.
- API ghi địa chỉ, version và audit/receipt trong một transaction. Không gửi trùng khi timeout; không ghi mật khẩu/token vào log.

## Giao diện

Hồ sơ dùng header tên/ảnh chữ cái đầu/trạng thái, thẻ hai cột, footer cố định. Khách hàng xem theo thẻ thông tin/địa chỉ/lịch sử; sửa hồ sơ có sổ địa chỉ. Nhân viên có ô địa chỉ liên hệ trong form sửa. Bộ lọc ca bỏ checkbox, hai ô ngày luôn dùng được, mặc định 30 ngày gần nhất, giới hạn trên bao gồm toàn bộ ngày kết thúc.

## Kiểm thử

CustomerAddressAdministrationTests: quyền, ownership, Web/Admin cùng dữ liệu, đổi mặc định, version cũ, chống trùng, ngừng dùng, rollback audit. EmployeeContactAddressTests: nhận diện/nâng cấp 35 cũ, bảo toàn mã và lưu cột 500 ký tự. ProfileAddressUiTests: nhập/lưu địa chỉ qua WinForms thật và ảnh các hồ sơ, xác nhận bộ lọc ngày không còn checkbox. Giữ các bài test hồi quy ADM-01/12 và nâng cấp 34.

Trong biểu thức LINQ EF, lời gọi constructor Employee phải truyền đủ tham số; endpoint POS truyền null cho DiaChiLienHe, không trả địa chỉ liên hệ cá nhân. Chưa thêm địa chỉ giả cho hồ sơ mẫu; người dùng lưu địa chỉ qua biểu mẫu mới.
