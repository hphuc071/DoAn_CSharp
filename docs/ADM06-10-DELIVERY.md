# Kết quả triển khai ADM-06 đến ADM-10 — 10/11/2026

## Phạm vi và quyết định

Bám `dac-25-Uc.docx`; giữ 5 UC, cấu trúc src hiện có, một project xUnit và thương hiệu 4CHANG LINH NGU LAM. Hướng dẫn nghiệp vụ/API/migration/phối hợp nhóm: [INTEGRATION-ADM-06-10.md](INTEGRATION-ADM-06-10.md). Danh sách file của đợt triển khai: [ADM06-10-FILES.txt](ADM06-10-FILES.txt); repo còn các thay đổi có sẵn từ trước, danh sách này không coi toàn bộ git diff là công việc mới.

| UC | Nội dung triển khai |
|---|---|
| ADM-06 | Nháp nhập/xuất/kiểm kê/điều chỉnh, sửa/xóa nháp, ghi sổ/đảo, chống vượt tồn và lấn giữ chỗ, mốc/version, audit/outbox nguyên tử |
| ADM-07 | Lọc/phân trang đa kênh, snapshot món/BOM/giữ/tiêu hao/thu-hoàn, lệnh Admin/POS chung, hủy phần chưa pha, chứng cứ đổi địa chỉ/giao lại/chốt thất bại, EXPIRED có đối chiếu |
| ADM-08 | Tra snapshot hóa đơn/phân trang, nguồn điều chỉnh hủy/khiếu nại duy nhất, PDF thật, preview/in/in lại Bản sao, không thu/phát hành lại khi in |
| ADM-09 | Nguồn/chứng cứ, duyệt giữ hạn mức, lần chi PROCESSING, đối chiếu thủ công SUCCEEDED/FAILED/RECONCILING, mã hóa tài khoản người nhận, đối soát tiền vào thiếu/thừa/muộn/chống ghép hai đơn |
| ADM-10 | Kỳ/múi giờ/mốc, tách doanh thu-thu-hoàn/ship/topping, tỷ lệ có mẫu số, top/cảnh báo, nguồn drill-down, audit có quyền riêng/mã nhân viên-ca-máy/correlation/che bí mật, job Excel/PDF có hủy/tải/mở lại |

## Kiểm chứng

- Build toàn solution và test project, không lỗi compile ở bản được kiểm tra.
- Lượt cuối `adm06-10-final-reviewed.trx`: **76/76 đạt**, 0 bỏ qua. Bao gồm 5 UC mới, WinForms ADM-01..10, BOM theo lịch và các kiểm thử đọc đơn khách hàng liên quan.
- Lượt hồi quy rộng `adm06-10-regression-verified.trx`: 151/152 đạt; một lượt đọc BOM gặp SQL deadlock 1205 trong lúc worker kích hoạt lịch. Kiểm thử polling được sửa để thử lại GET 503 trong deadline như thao tác Tải lại của UI, không retry POST hoặc chấp nhận dữ liệu cũ. Toàn bộ class BOM và các luồng bị tác động đều đạt trong lượt 76/76 cuối.
- Lượt hồi quy trước đó còn lỗi test đăng nhập UI quá nhiều chạm limiter; harness nay dùng lại token fixture đã đăng nhập thật. Không giảm limiter hoặc bypass xác thực phía server.
- WinForms được mở với API/SQL fixture thật, chụp desktop/compact, kiểm tra chiều cao hàng sau wrap, KPI đủ 8 thẻ, footer và thêm/bỏ dòng/lưu phiếu qua editor thật. Đã xem ảnh cả 5 trang mới và editor; giữ layout/sidebar/header và nâu-kem.
- PDF báo cáo 8 trang được render và xem từng trang: đúng tiếng Việt, 0 ký tự thay thế U+FFFD, đủ cột/đơn vị/mốc/số trang; đã sửa đường kẻ đè chữ và bảng rỗng. Excel là workbook OOXML thật, tiền là số, không có phần tử công thức từ dữ liệu người dùng.
- Test đều ở database GUID riêng. Ca bổ sung kiểm tra mất quyền audit rollback tồn/sổ/outbox, chi thất bại giữ nguyên điều chỉnh hóa đơn, thanh toán thiếu/đúng hạn/muộn, giao thất bại không nhập lại kho, thu CK nhiều kỳ theo từng giao dịch.

## Database đang dùng

- Backup trước nâng cấp đã có CHECKSUM và RESTORE VERIFYONLY hợp lệ: `/var/opt/mssql/backup/FourChangDb_before_ADM06_10_20261110_182524.bak`.
- Đã áp dụng migration được chấp thuận: **37 bảng nghiệp vụ, 74 FK**, giữ sysdiagrams.
- Đối chiếu trước/sau: 20 khách, 0 đơn, 0 hóa đơn, 0 hồ sơ hoàn, 1 chứng từ kho; tổng thu/hoàn vẫn 0. Không xóa hoặc tạo giao dịch mẫu trong database quán.
- Kiểm tra kết nối bằng fourchang_app và endpoint đọc kho/hóa đơn/hoàn/báo cáo bằng phiên Admin thật đều thành công.
- Log đối chiếu và tên backup trong `artifacts/run/adm06-10-*.txt` (ignored).

## Giới hạn cần biết

- Sao kê do Admin đối chiếu theo phương án đã chốt; chưa tích hợp API chi hoàn ngân hàng hay tự xác minh khoảng cách giao hàng.
- Worker hết hạn tự động chưa được bật khi thiếu nguồn đối soát ngân hàng; chốt EXPIRED thủ công có chứng cứ đã triển khai.
- Chưa kiểm thử máy in vật lý. PRINT_SENT ghi đã gửi spool, không khẳng định giấy đã in.
- Trang đơn Web hiện là snapshot tải lại; chưa có push/email/SMS. Sự kiện đã ghi outbox và reader tiền/trạng thái đồng bộ SQL, không tự mở lại các chức năng Web đang được nhánh nhóm khóa.
- Một số giao diện POS cũ còn dùng dữ liệu cục bộ; endpoint lệnh Staff chung đã cung cấp nhưng không tuyên bố đã thay toàn bộ luồng của thành viên khác.
- Chưa commit/push/merge GitHub thay nhóm. Không đưa .env/backup/tệp báo cáo vào Git.

## Tiếp tục ngày 11/11/2026

- Đã áp dụng bản sửa KPI còn dang dở: số tiền tự giảm nhẹ cỡ chữ để hiển thị đủ ở cửa sổ nhỏ, giữ nguyên layout và giá trị.
- Test giao diện có dữ liệu riêng tạo thực thu trên một triệu để tái hiện lỗi; ảnh compact đã xác nhận `1.260.000đ` đầy đủ. Không thêm dữ liệu vào database quán.
- Đã chuẩn hóa DateTimePicker của trang đơn, hóa đơn, báo cáo/nhật ký thành `dd/MM/yyyy`, không phụ thuộc định dạng vùng Windows; test kiểm tra trực tiếp Format/CustomFormat.
- Docker Desktop ban đầu không có engine hoạt động. Đã khởi động lại riêng tiến trình Docker, mở lại container SQL hiện có; không reset, không xóa distro/container/volume.
- Lượt test UI sau phục hồi: `adm06-10-kpi-verified.trx`, 11/11 đạt. Lượt kiểm tra ngày/layout cuối: `adm06-10-layout-final.trx`.
- Database hiện có được kiểm tra lại bằng fourchang_app: vẫn 37 bảng nghiệp vụ và 74 FK. Không chạy lại migration/reset dữ liệu trong lần tiếp tục này.
- Lượt kiểm tra layout/ngày cuối ngày 11/11: **5/5 đạt**, không bỏ qua. Build toàn solution: **0 lỗi, 0 cảnh báo**.
- Backend chưa khởi động được trong lần tiếp tục này: Windows đang dành riêng dải 5142–5241, bao gồm 5180/5181; Kestrel trả SocketException 10013. Đã xác minh 15880/15881 có thể bind; sau đó người dùng đã đồng ý đổi cổng local. Không sửa cấu hình mạng Windows.
## Kết nối local đã hoàn tất sau xác nhận đổi cổng

- Web: http://localhost:15880 ; API: http://localhost:15881 . Cả hai đang chạy; readiness trả ready/37 bảng.
- Đồng bộ `.env` (ignored), `.env.example`, helper `scripts/Get-LocalRunSettings.ps1`, lệnh backend/Web, mặc định ApiClient/LiveSession và cấu hình VS Code. Web nhận đúng API URL trong process, Admin/POS đọc cùng cấu hình local.
- Chạy lại backend khi đã ready trả thành công, không mở process trùng cổng. Không thay database/API nghiệp vụ/mật khẩu hay cấu hình mạng Windows.
- `tests/FourChang.Tests/TichHop/LocalRunSettingsTests.cs`: **6/6 đạt**, kiểm tra mặc định/override/thứ tự ưu tiên, chặn URL không phù hợp và không in bí mật .env.
- Build toàn solution **0 lỗi, 0 cảnh báo**. Smoke test thật: trang chủ, `/css/refresh.css`, proxy Web `/api/menu/1/variants` đều HTTP 200; API `/api/menu` đọc được dữ liệu.
- Lệnh hằng ngày giữ nguyên: `Chay-Web.cmd`, `Chay-QuanLy.cmd`, `Chay-NhanVien.cmd`; xem HUONG-DAN-CHAY.md tại thư mục gốc.