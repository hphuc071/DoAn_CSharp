# UC-ADM-03 — test C# xUnit

Dùng duy nhất `tests/FourChang.Tests/FourChang.Tests.csproj`. Không có script .ps1 làm test. Mỗi Fact tạo database GUID riêng, giữ nguyên database đồ án.

```powershell
dotnet test FourChang.sln --filter "UseCase=UC-ADM-03"
```

| Nhóm | File / test |
|---|---|
| Quyền 401/403, Staff không được ghi | MenuAdministrationTests.PhanQuyen_ChiAdminDuocGhiMenu |
| CRUD, xác nhận tác động, trùng tên/tổ hợp, version, xóa tham chiếu | CRUD_DanhMuc_Mon_CauHinh_Version_VaThamChieu |
| Giá chung Web/POS, giá/tùy chọn/giới hạn sai, receipt và chống trùng | GiaChung_TuyChon_Topping_Validation_VaReceipt |
| Lịch, banner, liên kết chạy mã, tồn kho hết | CongBo_Banner_Lich_VaKhaDungKho |
| Rollback audit/menu/outbox | AuditLoi_RollbackMenuVaOutbox |
| Worker mất quyền, phục hồi và lease hết hạn | Outbox_LoiPhat_ThuLai_VaLease |
| Mất phản hồi sau commit, offline không ghi | TimeoutSauCommit_TraReceipt_KhongTaoTrung |
| Snapshot món/topping/BOM của đơn không bị giá mới ghi đè | SuaGia_KhongDoiSnapshotDonCu |
| Chromium thật 375/768/1440px, giữ tìm/ghi chú, đổi giá/banner bằng SSE, XSS | MenuBrowserTests |
| WinForms STA thật, năm tab và năm editor, thêm danh mục, tìm/tải lại, POS chọn cấu hình | MenuUiTests |
| Nâng cấp 34 → 35 → 37, sysdiagrams, PK/FK/CHECK/trigger/rowversion | TichHop/DatabaseTests |

Ảnh/log/TRX tại artifacts/tests. Các ảnh fixture thể hiện dữ liệu kiểm thử, không phải doanh thu hoặc thay đổi database thật.
