# UC-ADM-09 — Xử lý và xác nhận hoàn tiền

Nguồn: `dac-25-Uc.docx`, giữ nguyên đánh số 25 UC. Quy tắc: BR-04, BR-08, BR-14, BR-18, BR-19, BR-20, BR-21, BR-22, BR-26, BR-29, BR-36.

| Test xUnit | Kết quả cần bảo đảm |
|---|---|
| `Duyet_DangChi_DoiChieuThuCong_MoiTinhDaHoan` | PROCESSING chưa tăng R; thủ công xác minh mới SUCCEEDED, chống trùng. |
| `HaiHoSo_KhongDuyetVuotThu_ChuaRoKhongChiLai` | Hạn mức giữ ngăn vượt thu; RECONCILING không cho chi lại. |
| `TienThieu_KhongNhan_DuTienMoiNhan_KhongCongLap` | Thiếu chưa nhận; giao dịch cùng nguồn không cộng lại. |
| `TienMuon_Expired_KhongHoiSinh_KhongNhapKho` | EXPIRED, nguồn LATE_PAYMENT; giải phóng giữ chỗ không tăng tồn. |
| `ChotHetHan_CanChungCu_DaThuDuKhongChoChot` | Cần sao kê; đã thu đủ không cho EXPIRE. |

Test UI chung: `QuanLy/DungChung/OperationsUiTests.cs`, mở WinForms thật với API/SQL, chụp desktop/compact, kiểm tra wrap nút và chiều cao báo cáo; ADM-06 kiểm tra thêm/bỏ dòng và lưu nháp qua form thật.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-09"
```

Database GUID riêng trong Docker; không chạy reset/seed trên database quán. Test không thay thế kiểm thử máy in thật và sao kê ngân hàng thực tế.
