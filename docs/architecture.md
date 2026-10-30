# Kiến trúc và quy ước mở rộng

## Ranh giới hiện tại

```text
Web / Razor Pages ───────┐
                        ├── Presentation.Shared (UI records, catalog, demo fixtures)
POS / Admin → Desktop UI ┘
```

Web, POS và Admin là ba executable độc lập. POS/Admin cùng tham chiếu thư viện FourChang.Desktop để tái sử dụng UI. Shared không biết Razor, Control, HTTP, EF hay SQL. Các lớp `*View` tạo control và trình bày; `Ui` tạo control chuẩn; `PreviewDialog` chỉ mở bản xem trước. Web JS chỉ xử lý DOM, không có fetch hoặc lưu dữ liệu. Không có mật khẩu/API key/connection string trong mã nguồn.

## Vì sao tổ chức như vậy

| Tiêu chí | Quy ước cụ thể |
|---|---|
| Dễ quản lý | Tách Web/Desktop/Shared; màn hình theo actor và feature; mã UC tập trung trong ScreenCatalog. |
| Dễ mở rộng | Thêm feature riêng, đăng ký vào Shell/ScreenCatalog; dùng lại control/partial và theme. |
| Dễ bảo trì | Màu, font, bảng, field, dialog nằm ở một nơi trên mỗi nền tảng; fixture tách khỏi UI. |
| Dễ tái sử dụng | Shared chỉ chứa record hiển thị độc lập framework; asset dùng chung, mỗi executable copy khi build. |
| Dễ tích hợp | Thay fixture bằng adapter/application service sau này; không cho Desktop truy cập trực tiếp SQL. |

## Khi bắt đầu triển khai nghiệp vụ (chưa tạo ở bản này)

Thêm `FourChang.Domain` cho entity/value object/rule và state machine; `FourChang.Application` cho use case, interface repository/payment/refund/inventory; `FourChang.Infrastructure` cho EF Core SQL Server và adapter VietQR; `FourChang.Api` làm điểm vào duy nhất cho hai UI. Có thể thêm `FourChang.Contracts` cho request/response/version DTO. Những project này chỉ nên tạo khi bắt đầu chức năng thật, tránh đưa nghiệp vụ giả vào bản UI.

```text
Web / Desktop → API (HTTP + SignalR)
                    ↓
               Application → Domain
                    ↑
             Infrastructure → SQL Server / payment integration
```

Domain không phụ thuộc UI/EF; Application không phụ thuộc WinForms/Razor. Infrastructure hiện thực interface Application; API cấu hình DI. Model `ProductPreview` không dùng làm EF entity hoặc contract công khai. Ánh xạ DTO sang view model qua adapter riêng theo feature.

## Hợp đồng tích hợp cần giữ theo đặc tả

- Web chỉ nhận chuyển khoản VietQR trả trước; tạo QR không đồng nghĩa đã nhận tiền. Adapter nhận giao dịch/xác minh ngân hàng riêng, không suy luận từ hình QR. POS cho CASH hoặc CK đã xác minh; không thu lần hai đơn Web.
- Giữ chỗ khác tiêu hao thực tế: ghi nhận giữ chỗ khi chấp nhận gửi đơn hợp lệ; trừ BOM một lần khi bắt đầu pha từng phần, theo đặc tả gốc. Hoàn tất không trừ lần hai. Snapshot công thức/giá giữ theo dòng đơn. Phần chưa pha được giải phóng giữ chỗ khi hủy; hoàn tiền không tự hoàn kho đã tiêu hao.
- Đường/đá có mapping cấu trúc theo BOM; ghi chú tự do không tự thay định mức. Quy tắc hết hàng xét biến thể/topping và tồn khả dụng. Backend tính lại mọi giá/phí/khả dụng.
- Customer chỉ yêu cầu hủy khi Chờ xác nhận; Staff hủy số lượng chưa bắt đầu pha, có lý do. Refund có mã riêng, hạn mức còn được hoàn, người duyệt, bằng chứng và xác minh chi; timeout dùng trạng thái đối soát, không tự chi lại.
- Tên nhân viên tự khai luôn đi cùng ShiftId/TerminalId; không dùng tên thay danh tính được xác thực. Máy POS và tập quyền cần được Admin quản lý. Admin có cơ chế xác thực riêng, quyền audit/xuất/duyệt hoàn độc lập.
- Mọi lệnh thay đổi cần idempotency key, version/concurrency, transaction và audit. SignalR chỉ thông báo thay đổi; sau mất kết nối phải đọc snapshot/version mới. Không xác nhận lệnh ghi offline; trạng thái chưa rõ phải tra cứu trước retry.
- UI cung cấp chỗ nhập/lỗi/trạng thái; các invariant và chính sách được kiểm tra ở API. Chưa có cơ chế bảo mật sản xuất trong bản mẫu.

## Quy ước mã nguồn

Đặt tên `TênFeatureView.cs` trong nhóm POS/Admin; không nhét SQL/API vào handler Click. Khi tích hợp, handler gọi presenter/service qua interface và render state Loading/Success/Empty/Error/Offline. Không sửa fixture dùng chung khi chỉ cần mẫu riêng một UC. Dùng mã UC trong tiêu đề feature để truy vết tới tài liệu.

WinForms được xây từ control C# tái sử dụng, không phải bộ Form kéo thả `.Designer.cs`. Chỉnh bố cục tại `Shell/MainForm.cs`, `Controls/Ui.cs` và từng feature. Nếu cần Visual Studio Designer trong giai đoạn sau, có thể chuyển riêng feature thành `UserControl` partial mà không thay project và dữ liệu hiển thị.
