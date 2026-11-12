# UC-ADM-08 — Tra cứu và quản lý hóa đơn

Nguồn: `dac-25-Uc.docx`, giữ nguyên đánh số 25 UC. Quy tắc: BR-08, BR-13, BR-14, BR-20, BR-21, BR-22, BR-29, BR-31, BR-36.

| Test xUnit | Kết quả cần bảo đảm |
|---|---|
| `SnapshotBatBien_PdfKhongPhatHanhLai` | PDF lấy snapshot, không tạo thêm hóa đơn/thu tiền. |
| `DieuChinhCoNguon_KhongTrung_KhongSuaBanGoc` | Hai operation cùng nguồn chỉ có một điều chỉnh, không sửa gốc. |
| `KhieuNai_DuyetMoiDieuChinh_KhongGiamNghiaVuHaiLan` | Chưa duyệt chặn điều chỉnh; FK nguồn hoàn; không giảm nghĩa vụ lần hai; FAILED không đảo điều chỉnh đã phát hành, duyệt lại vẫn đúng nghĩa vụ. |

Test UI chung: `QuanLy/DungChung/OperationsUiTests.cs`, mở WinForms thật với API/SQL, chụp desktop/compact, kiểm tra wrap nút và chiều cao báo cáo; ADM-06 kiểm tra thêm/bỏ dòng và lưu nháp qua form thật.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-08"
```

Database GUID riêng trong Docker; không chạy reset/seed trên database quán. Test không thay thế kiểm thử máy in thật và sao kê ngân hàng thực tế.
