# Đối chiếu giao diện với 24 Use Case

Nguồn: `references/Dac_ta_chi_tiet_24_Use_Case_Sau_khi_gop.docx`. Mã UC giữ nguyên. Bảng dưới mô tả **phạm vi thiết kế UI**, không tuyên bố nghiệp vụ đã được hiện thực. Không tách thêm actor tự động; trạng thái tích hợp được đặt trong màn hình Customer/Staff/Admin liên quan.

## Khách hàng — 8 UC

| UC | Màn hình / tệp | Luồng chính thể hiện trên UI | Nhánh thay thế / lỗi để review |
|---|---|---|---|
| UC-WEB-01 Đăng ký | `/Account/Register` · Pages/Account/Register.cshtml | Họ tên, email/SĐT, mật khẩu/xác nhận, điều khoản, đăng ký, về đăng nhập. | Form required, nhánh định danh trùng/chính sách mật khẩu, hủy; lỗi/mất mạng ở thanh trạng thái mẫu. |
| UC-WEB-02 Đăng nhập và đăng xuất | `/Account/Login`, `/Account/Profile` | Định danh, mật khẩu, nút đăng nhập; đăng xuất ở hồ sơ. | Sai mật khẩu, khóa tài khoản, đổi mật khẩu tạm; hết phiên/mất mạng được biểu diễn bằng thông báo mẫu. |
| UC-WEB-03 Thông tin cá nhân | `/Account/Profile` · Account/Profile.cshtml | Tab hồ sơ, sổ địa chỉ, mật khẩu; thêm/sửa/xóa địa chỉ, mặc định, lưu. | Hủy chỉnh sửa; form mật khẩu xác nhận; lỗi dữ liệu/phiên/mất mạng chưa lưu. |
| UC-WEB-04 Xem và tìm thực đơn | `/` · Index.cshtml | Banner, món mới/bán chạy, tìm theo tên, danh mục, lọc giá/sắp xếp, xem món và còn/hết. | Không có kết quả, đặt lại lọc; món tạm hết vô hiệu thêm; loading/offline/error mẫu. |
| UC-WEB-05 Tùy chỉnh và giỏ hàng | `/`, `/Cart` · Shared/_ProductCustomizer.cshtml, Cart/Index.cshtml | Size S/M/L, nóng/lạnh, đường/đá, topping, lượng, ghi chú; sửa/xóa/tăng giảm dòng giỏ và tổng mẫu. | Chỉnh món bằng hộp thoại, xác nhận xóa; giỏ rỗng/món hết/giá đổi qua thông báo mẫu. Không tính tiền nghiệp vụ. |
| UC-WEB-06 Đặt món đa kênh | `/Checkout` · Checkout/Index.cshtml | Tab tại bàn nhận B05 từ QR mẫu, mang đi có người nhận/giờ, giao hàng có địa chỉ/định vị/phí/tổng mẫu. | Đổi hình thức; QR bàn lỗi/hết hiệu lực, ngoài vùng giao, không báo được phí, yêu cầu đăng nhập và hết kho qua preview. |
| UC-WEB-07 VietQR | `/Checkout/Payment` · Checkout/Payment.cshtml | Số tiền, mã đơn/nội dung chuyển, QR giữ chỗ, hạn thanh toán, kiểm tra trạng thái. Website chỉ CK trả trước. | Bộ trạng thái chờ/đủ/thiếu/thừa/hết hạn/đối soát; không dùng ảnh chuyển khoản tự khai để đánh dấu đã trả; không COD. |
| UC-WEB-08 Đơn cá nhân, lịch sử, hủy/hoàn | `/Orders` · Orders/Index.cshtml | Tab đang xử lý/lịch sử/hoàn tiền; timeline, chi tiết, hóa đơn; hủy Chờ xác nhận có lý do. | Đã pha không cho tự hủy; xác nhận hủy, trạng thái hoàn/đối soát, theo dõi mất kết nối và lịch sử rỗng. |

## Nhân viên — 5 UC

Các tệp ở `FourChang.Desktop/Features/Pos`. Mở từ sidebar POS. Dữ liệu ca mẫu luôn kèm mã ca/máy. Không có tài khoản hoặc mật khẩu Staff.

| UC | Màn hình / tệp | Luồng chính thể hiện trên UI | Nhánh thay thế / lỗi để review |
|---|---|---|---|
| UC-POS-01 Ca làm việc | EntryForm → bắt đầu ca; `ShiftView.cs` | Nhập tên và tiền đầu ca; đang ca có mã máy/ca; khóa, tiếp tục, tiền thực đếm, chênh lệch, xem chốt ca. | Bàn giao có người nhận/lý do, khôi phục sau sự cố, khoản thu chưa rõ không cộng tiền mặt; chưa tạo ca thật. |
| UC-POS-02 Tạo đơn tại quầy | Bán hàng · `SaleView.cs` | Menu, cấu hình chi tiết món, giỏ POS, tại bàn/mang đi, ghi chú, tổng, tạo đơn nháp. | Chọn bàn, sửa tùy chọn, xóa nháp, món tạm hết disabled; không thu lại đơn Web. |
| UC-POS-03 Tiếp nhận và xử lý | Đơn hàng · `OrdersView.cs` | Bộ lọc/tra cứu, chi tiết món/tùy chọn, tiếp nhận, bắt đầu pha từng phần, sẵn sàng, bàn giao giao hàng, hoàn tất. | Từ chối có lý do, cạnh tranh/version, giao thất bại, mất kết nối; Web phải xác minh đủ tiền trước tiếp nhận. |
| UC-POS-04 Hủy phần chưa pha | Hủy món · `CancelView.cs`; dialog từ bán/đơn | Dòng món, đã pha/chưa pha, chọn lượng còn được hủy, lý do, số hoàn dự kiến và xác nhận mẫu. | Phần đang pha bị chặn; số lượng/lý do không hợp lệ; cập nhật cạnh tranh, Web chuyển nghĩa vụ hoàn. |
| UC-POS-05 Thu ngân và hóa đơn | Thu tiền và hóa đơn · `PaymentView.cs` | Tab CASH với tiền khách đưa/tiền thừa; CK với trạng thái xác minh; hóa đơn, xem/in/in lại/PDF. | Thiếu tiền, CK chưa rõ, đổi cách trả, in lỗi sau khi đã thu; đơn Web chỉ xem chứng từ, không thu lần nữa. |

## Quản lý — 11 UC

Các tệp ở `FourChang.Desktop/Features/Admin`. CRUD dùng bảng + các hộp thoại theo trường của hành động. Không thêm tài khoản Staff vào CRUD. Đăng nhập Admin có biểu mẫu riêng ở EntryForm; sidebar Phân quyền có quản lý Admin/vai trò/máy POS.

| UC | Màn hình / tệp | Luồng chính thể hiện trên UI | Nhánh thay thế / lỗi để review |
|---|---|---|---|
| UC-ADM-01 Khách hàng | `CustomersView.cs` | Tìm hồ sơ, thêm/sửa, xem lịch sử, khóa/mở, reset mật khẩu tạm, xóa có điều kiện. | Hồ sơ có giao dịch không xóa vật lý; trùng định danh/lý do; khóa không xóa nghĩa vụ đơn/hoàn. |
| UC-ADM-02 Quản trị và phân quyền | `AccessView.cs` | Tab tài khoản Admin, vai trò/quyền, thiết bị POS; tạo/sửa/khóa/reset Admin, đăng ký/thu hồi máy. | Không khóa Admin cuối; phân biệt quyền audit/xuất/hoàn; thu hồi máy phải xử lý ca; tự khai tên không cấp quyền Admin. |
| UC-ADM-03 Thực đơn | `MenuManagementView.cs` | Tab món/giá size/tùy chọn, danh mục, topping/giá/BOM, banner; CRUD và bật/tắt bán. | Dữ liệu đang dùng chỉ ngừng bán; không dùng giá mới sửa đơn cũ; trường giá/cấu hình/lịch banner cần kiểm tra. |
| UC-ADM-04 Nguyên liệu | `IngredientsView.cs` | Bảng mã/tên/đơn vị cơ sở/quy đổi/ngưỡng, thêm/sửa/ngừng/xóa có điều kiện. | Không sửa tồn trực tiếp; đơn vị sai/quy đổi không dương, nguyên liệu có BOM/chứng từ không xóa lịch sử. |
| UC-ADM-05 BOM | `RecipesView.cs` | Chọn món-size-nóng/lạnh/topping; định mức, đường/đá cấu trúc, phiên bản hiệu lực, sao chép/mô phỏng/kích hoạt. | Ghi chú ngoài cấu trúc không tự đổi định mức; lỗi định mức/phiên bản, snapshot đơn cũ; không ghi đè công thức đã dùng. |
| UC-ADM-06 Tồn kho và khả dụng | `InventoryView.cs` | KPI, thực tế/giữ chỗ/an toàn/khả dụng, sổ kho, cảnh báo, món bị ảnh hưởng; lối sang chứng từ kho. | Khả dụng = thực tế − giữ chỗ − an toàn; không sửa trực tiếp, không suy luận offline là còn hàng; cảnh báo theo biến thể. |
| UC-ADM-07 Nhập/xuất/điều chỉnh | `StockDocumentsView.cs` | Tab danh sách/nhập/xuất/kiểm kê; đối tác/ngày, dòng/đơn vị/quy đổi, nháp, ghi sổ, chênh lệch và phê duyệt. | Chỉ xóa nháp; đã ghi sổ dùng đảo có lý do; thiếu tồn, kiểm kê snapshot cũ, version và ghi sổ trùng. |
| UC-ADM-08 Giám sát đơn | `OrdersManagementView.cs` | Lọc đa kênh, chi tiết pha chế, truy vết ca/máy, địa chỉ/phí, tiếp nhận và xử lý phần còn lại. | Hủy phần chưa pha, đổi địa chỉ theo điều kiện, giao thất bại và quyết định xử lý; không tua lùi phần đã tiêu hao. |
| UC-ADM-09 Hóa đơn và đối chiếu | `InvoicesView.cs` | Lịch sử, nguồn tiền, đối chiếu chứng từ, xem chi tiết, điều chỉnh liên kết, in lại/xuất. | Không sửa hóa đơn đã phát hành; điều chỉnh có chứng từ riêng; quyền xuất/in, timeout và không thu trùng. |
| UC-ADM-10 Hoàn tiền | `RefundsView.cs` | Danh sách yêu cầu, hạn mức, duyệt, tài khoản nhận, bằng chứng chi, xác minh và đối soát. | Thiếu/thừa/tiền đến muộn, người nhận sai, thất bại/timeout; không nhấn là đã chuyển tiền; từ chối không xóa nghĩa vụ còn nợ. |
| UC-ADM-11 Thống kê và audit | Tổng quan + `ReportsView.cs`, `DashboardView.cs` | Dashboard doanh thu, tách TM/CK, hoàn, tỷ lệ đơn, top món, tồn sắp hết; lọc kỳ/nguồn/ca, Excel/PDF. Tab audit có trước/sau và correlation. | Kỳ rỗng/mẫu số 0, mất mạng/lỗi tải, quyền báo cáo khác audit/xuất; nhật ký chỉ xem/xuất, không sửa/xóa. |

## Trạng thái chung và phạm vi nghiệm thu

| Nhóm | Cách xem | Giới hạn bản UI |
|---|---|---|
| Normal / Empty / Offline / Error / Loading | Chọn trạng thái trên Web; tìm kiếm không khớp để xem menu rỗng. Desktop chọn trạng thái để xem thông báo. | Không giả lập đầy đủ hạ tầng hoặc mọi lỗi server. Desktop vẫn giữ dữ liệu mẫu phía sau thông báo. |
| Thay thế nghiệp vụ | Tab nhận món, hồ sơ, thanh toán, báo cáo, chứng từ; nút preview theo từng hành động. | Sự kiện không lưu dữ liệu hoặc thực hiện state transition nghiệp vụ. |
| Lỗi theo UC | Nút xem trường hợp đặc biệt và mô tả điều kiện trong hộp thoại. | Validation phía server/transaction/rollback thuộc giai đoạn chức năng sau. |
| Quyền và trạng thái nhạy cảm | Thông tin quyền, disabled cho món hết/phần không được hủy, lý do/bằng chứng trong form mẫu. | Chưa có security boundary thật; không dùng để vận hành quán. |

Tất cả 24 UC có điểm truy cập thiết kế. Chỉ các tương tác trình bày được thực thi. Những bước cần hệ thống/API/DB được biểu diễn bằng trường, trạng thái và thông báo review, giữ đúng yêu cầu chỉ thiết kế giao diện.
