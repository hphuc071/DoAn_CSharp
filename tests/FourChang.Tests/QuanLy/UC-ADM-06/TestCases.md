# UC-ADM-06 — Quản lý chứng từ kho

Nguồn: `dac-25-Uc.docx`, giữ nguyên đánh số 25 UC. Quy tắc: BR-02, BR-03, BR-05, BR-08, BR-14, BR-23, BR-25, BR-26, BR-27, BR-28, BR-29.

| Test xUnit | Kết quả cần bảo đảm |
|---|---|
| `NhapKho_NhapKhongDoiTon_GhiSoMotLan_DaoCoLienKet` | Nháp không đổi tồn; ghi sổ, đảo chống lặp; gốc bất biến. |
| `XuatVuotTon_ChanToanPhieu_VaDoiNoiDungMaCuBiChan` | Một dòng vượt tồn rollback toàn phiếu; mã cũ không nhận nội dung khác. |
| `KiemKe_MocDaDoiBiChan_ChenhLechKhongKhongTaoBienDong` | Mốc cũ chặn chốt; khớp không sinh biến động giả. |
| `Nhap_SuaVaXoa_KhongDoiTon` | Sửa/xóa nháp theo version; không dòng mồ côi. |
| `LoiAudit_KhongGhiSoMotPhan` | Chặn quyền audit trong fixture, kiểm tra rollback tồn/sổ/outbox. |
| `Kho_YeuCauPhienAdmin` | 401/403 khi không có phiên Admin. |

Test UI chung: `QuanLy/DungChung/OperationsUiTests.cs`, mở WinForms thật với API/SQL, chụp desktop/compact, kiểm tra wrap nút và chiều cao báo cáo; ADM-06 kiểm tra thêm/bỏ dòng và lưu nháp qua form thật.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-06"
```

Database GUID riêng trong Docker; không chạy reset/seed trên database quán. Test không thay thế kiểm thử máy in thật và sao kê ngân hàng thực tế.
