# Tích hợp UC-ADM-06 đến UC-ADM-10

Nguồn nghiệp vụ: `dac-25-Uc.docx`. Tài liệu này sử dụng số UC mới của bản 25 UC: ADM-06 kho, ADM-07 đơn hàng, ADM-08 hóa đơn, ADM-09 hoàn tiền, ADM-10 báo cáo/nhật ký. Không dùng lại số UC của bản 26 UC cũ.

## Các quyết định đã được chấp thuận

1. Giữ **37 bảng**. Thêm bốn cột, một FK và các ràng buộc nguồn điều chỉnh, không xóa dữ liệu cũ:

| Bảng | Bổ sung | Ý nghĩa |
|---|---|---|
| DonHang | MaXuLyNgoaiLe | Mã xử lý giao thất bại/hết hạn; tối đa 30 ký tự |
| HoSoHoanTien | LoaiNguon | LEGACY / CANCELLATION / OVERPAYMENT / LATE_PAYMENT / COMPLAINT |
| HoSoHoanTien | ChungCuYeuCau | Tham chiếu chứng cứ yêu cầu, tối đa 500 ký tự |
| HoaDon | MaHoanTien | FK nguồn điều chỉnh khiếu nại; nguồn hủy dùng MaHuyMon đã có |

2. Thêm `EXPIRED` cho **đơn hàng** Web hết hạn chờ tiền, không quy thành khách chủ động hủy. Không tự đổi trạng thái đơn cũ. EXPIRED không đồng nghĩa tự xác minh ngân hàng.
3. Hoàn tiền xác minh **thủ công bằng sao kê/chứng từ đáng tin cậy**. Nhập mã chuyển khoản chỉ ghi PROCESSING; SUCCEEDED cần bước xác nhận riêng của Admin. Không yêu cầu Admin thứ hai và không giả lập API hoàn tiền.
4. Hồ sơ hoàn cũ chưa rõ nguồn để LEGACY và cần bổ sung/đối soát; không tự gán nguồn hủy. Hóa đơn điều chỉnh trùng nguồn khiến migration dừng, không tự xóa/gộp chứng từ.

## Phạm vi mã nguồn

```text
src/
├─ Backend/
│  ├─ FourChang.Api/
│  │  ├─ Features/Admin/
│  │  │  ├─ StockDocuments/       # ADM-06: nháp, ghi sổ, đảo
│  │  │  ├─ Orders/               # ADM-07: truy vấn, địa chỉ, lệnh
│  │  │  ├─ Invoices/             # ADM-08: bản gốc, điều chỉnh, PDF/log in
│  │  │  ├─ Refunds/              # ADM-09: nguồn, duyệt, lần chi, đối soát
│  │  │  └─ Reports/              # ADM-10: số liệu, audit, snapshot, job
│  │  └─ Shared/
│  │     ├─ Orders/               # Nghĩa vụ tiền và lệnh dùng chung Admin/POS
│  │     └─ Documents/            # XLSX/PDF từ snapshot
│  ├─ FourChang.Domain/           # Thuộc tính mới đã được chấp thuận
│  └─ FourChang.Infrastructure/   # Mapping, kiểm tra schema và migration
├─ Presentation/FourChang.Desktop/
│  ├─ Admin/{StockDocuments,Orders,Invoices,Refunds,Reports}/
│  └─ Shared/Controls/Admin/      # Dialog typed, giữ request khi timeout, nhãn Việt
└─ Shared/FourChang.Presentation.Shared/
   ├─ Contracts/Admin/            # DTO dùng cùng tên/giá/version
   └─ ApiClients/Admin/           # Client dùng chung, tải tài liệu có kiểm tra

tests/FourChang.Tests/QuanLy/
├─ UC-ADM-06/ ... UC-ADM-10/      # .cs xUnit và TestCases.md từng UC
└─ DungChung/                    # SQL fixture, test form/ảnh đối chiếu
```

Mỗi phương thức trong các file triển khai mới có chú thích tiếng Việt nêu mục đích; transaction, snapshot, giữ hạn mức, kiểm tra version và xử lý timeout có giải thích tại chỗ. Không tạo project test mới.

## Cách các thành viên sử dụng chung

| Phần việc | Điểm tích hợp | Quy tắc |
|---|---|---|
| Khách hàng | Reader đơn, thu và hoàn trong cùng SQL | Giá từ snapshot; EXPIRED đọc được; chỉ hiện khoản hoàn thành công là đã hoàn |
| Nhân viên | `POST /api/pos/orders/{id}/actions` | Dùng `OrderActionInput` và version; yêu cầu phiên Staff/ca hợp lệ theo cơ chế xác thực hiện có |
| Quản lý | `/api/admin/orders/{id}/actions` | Cùng `OrderWorkflowService`; giao thất bại/hết hạn chỉ Admin |
| Kho | `/api/admin/stock-documents` | Nháp không đổi tồn; ghi sổ cùng sổ/audit/outbox; không viết trực tiếp TonKho từ UI |
| Hóa đơn | `/api/admin/invoices` | Chỉ điều chỉnh có nguồn; xem/in/PDF không tạo khoản thu hay chứng từ gốc mới |
| Hoàn tiền | `/api/admin/refunds` | Không gọi SQL cập nhật R từ Web/POS; không chi lại lần đang chưa rõ kết quả |
| Báo cáo | `/api/admin/reports` | Snapshot theo mốc; job gắn người dùng và kiểm tra quyền lại khi tải |

**Thời điểm kho:** giữ chỗ khi đặt theo luồng hiện có; chuyển giữ chỗ thành tiêu hao khi START pha, không trừ lại FINISH/COMPLETE. Hủy chỉ phần chưa pha, giải phóng đúng phần giữ. Nguyên liệu đã pha không được tự nhập lại khi hoàn tiền/giao thất bại.

**Đồng bộ:** các lệnh mới ghi `SuKienDongBo` cùng transaction; worker công bố chỉ sau commit. Catalog Web/POS tiếp tục đọc nguồn giá và khả dụng chung. Trang đơn của khách trong nhánh hiện tại là snapshot tải lại, chưa có thông báo push chủ động; không tuyên bố đã triển khai email/SMS hay tự đổi toàn bộ luồng của thành viên khác. Khi tích hợp UI POS cũ còn dùng dữ liệu cục bộ, thay nơi gọi bằng API chung trên; không sao chép quy tắc kho/tiền vào UI.

## Nâng cấp database và chạy

Từ thư mục gốc repo, sau khi đồng bộ source và cấu hình `.env` riêng từng máy:

```powershell
dotnet restore FourChang.sln
dotnet build FourChang.sln --no-restore
dotnet run --project src/Backend/FourChang.Api/FourChang.Api.csproj --no-build -- --init-db
dotnet run --project src/Backend/FourChang.Api/FourChang.Api.csproj --no-build -- --check-db
```

Trước nâng cấp database có dữ liệu: dừng API đang ghi, backup SQL Server và xác minh backup. `--init-db` kiểm tra đúng schema cũ trước khi áp dụng `docs/database/37-bang/006-admin-operations-and-finance.sql`. Không chạy EnsureDeleted/reset/reseed để thay migration. Sau nâng cấp phải là **37 bảng nghiệp vụ, 74 FK**; `sysdiagrams` giữ nguyên và không tính bảng nghiệp vụ.

Khởi động backend bằng `Chay-Backend.cmd`; mở quản lý bằng `Chay-QuanLy.cmd` sau khi backend ready. Các route menu hiện có: Chứng từ kho, Đơn hàng, Hóa đơn, Hoàn tiền, Báo cáo và nhật ký.

Không đưa `.env`, backup, tệp báo cáo chứa dữ liệu hay khóa DataProtection vào Git. Báo cáo/job lưu mặc định tại `artifacts/reports/<tên database>/`, đã nằm trong thư mục ignored. Máy triển khai cần quyền ghi cho process API và hạn chế người khác đọc thư mục này. Giữ kho khóa ASP.NET DataProtection khi chuyển máy nếu cần đọc lại thông tin tài khoản người nhận đã mã hóa.

## Cấu hình và giới hạn tích hợp

| Cấu hình | Ý nghĩa |
|---|---|
| Reports:StoragePath | Thư mục gốc lưu snapshot/job/tệp; tự tách thư mục theo database |
| Reports:FontPath | TTF Unicode để nhúng PDF; Windows dùng Arial hiện có, Linux có DejaVu Sans nếu được cài |
| AdminPermissions:ReportsRead | Quyền đọc báo cáo; đặt false để từ chối |
| AdminPermissions:ReportsExport | Quyền xuất báo cáo |
| AdminPermissions:AuditRead | Quyền đọc audit độc lập báo cáo |
| AdminPermissions:AuditExport | Quyền xuất audit |
| Operations:WaitingWarningMinutes | Ngưỡng cảnh báo chờ xác nhận; không tự đặt SLA khi chưa cấu hình |

Các cấu hình là server-side, không nhận từ request/UI. Khi không đặt cờ quyền, người có vai trò Admin được dùng tính năng; Staff/Customer vẫn bị policy chặn trước đó. Đọc audit không phụ thuộc quyền báo cáo.

PDFsharp 6.2.4 dùng cho file PDF thật, nhúng font Unicode; Excel tạo OOXML (.xlsx), tiền là số, mã/nội dung người dùng là text, không sinh công thức từ tên/lý do. File chỉ tải khi READY. Đóng dialog có thể mở lại ở **Tệp đã xuất**; không tạo snapshot mới để giả nối lại công việc cũ.

API ngân hàng tự động, xác minh khoảng cách bản đồ và nhánh API hoàn tiền chưa được cấu hình trong repo hiện tại. Không bật worker hết hạn tự động khi nguồn đối soát không sẵn sàng. Admin có thể chốt EXPIRED sau hạn bằng bước đối chiếu sao kê bắt buộc. Thu đủ đúng hạn nhưng đối soát muộn không tự coi thành tiền muộn; đối chiếu theo thời điểm giao dịch ngân hàng.

Hóa đơn điều chỉnh khiếu nại đã phát hành tiếp tục làm giảm nghĩa vụ kể cả lần chi bị FAILED; thất bại chỉ giải phóng giữ hạn mức, không tự đảo chứng từ. Duyệt lại cùng hồ sơ phải đúng khoản điều chỉnh đó. Admin, reader khách hàng và báo cáo cùng loại trừ việc giảm hai lần.

Máy in: code có preview, PrintDialog, log gửi/in lại và dấu Bản sao; việc gửi spool không chứng minh giấy đã in. Cần kiểm tra bằng máy in thực tế, không khẳng định đã kiểm thử phần cứng.

## Kiểm thử và phối hợp Git

```powershell
# Năm UC, SQL fixture riêng
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-06|UseCase=UC-ADM-07|UseCase=UC-ADM-08|UseCase=UC-ADM-09|UseCase=UC-ADM-10" --logger "trx;LogFileName=adm06-10.trx"

# Giao diện WinForms thật, cần Windows + Docker SQL
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "Feature=ADM06-10-UI"
```

Test tạo database GUID riêng, không xóa dữ liệu quán. Đọc `tests/test-manifest.json` và `TestCases.md` từng UC để biết phạm vi. File TRX trong `tests/FourChang.Tests/TestResults`; log/PNG/XLSX/PDF trong `artifacts/tests/xunit`.

Trước merge, thống nhất một người xử lý các file chung: `Program.cs`, `CafeDbContext.cs`, `DatabaseBootstrap.cs`, `AdminOperation.cs`, `DatabaseViews.cs`, các DTO reader đơn và `schema.sql/model.json`. Giữ các đăng ký endpoint/service của cả hai nhánh, chỉ chạy migration schema đã chốt, không ghi giá riêng tại Web/POS. Merge nhánh tính năng vào nhánh tích hợp của nhóm rồi chạy test lại trước PR main. Chưa tự commit/push/merge thay nhóm.

## URL local cập nhật ngày 11/11/2026

Đã được người dùng chấp thuận: Web `http://localhost:15880`, API `http://localhost:15881` vì cổng cũ nằm trong dải Windows dành riêng. Helper `scripts/Get-LocalRunSettings.ps1` chỉ đọc hai khóa URL và không in bí mật .env. Backend, Web, Admin/POS đã đồng bộ URL; không thay endpoint nghiệp vụ hoặc schema. Các thành viên cập nhật `.env` riêng theo hai khóa trong `.env.example`, không sao chép/upload mật khẩu của người khác.