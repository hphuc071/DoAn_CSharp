# UC-ADM-10 — Thống kê, báo cáo và tra cứu nhật ký hệ thống

Nguồn: `dac-25-Uc.docx`, giữ nguyên đánh số 25 UC. Quy tắc: BR-05, BR-08, BR-19, BR-21, BR-22, BR-26, BR-29, BR-30, BR-31, BR-35, BR-36.

| Test xUnit | Kết quả cần bảo đảm |
|---|---|
| `DoanhThuVaDongTien_TachRieng_KyRongKhongCoTyLeGia` | Tách doanh thu/thực thu; kỳ rỗng tỷ lệ null. |
| `JobXuat_DungSnapshot_KhongCoFileDo (EXCEL/PDF)` | Job thật, download READY, mở lại job cũ, XLSX không có công thức. |
| `ThuNhieuDot_KhacKy_KhongDonVaoLanXacMinhCuoi` | Từng giao dịch CK vào đúng kỳ, không tính theo lần xác minh cuối. |
| `Audit_CheBiMat_QuyenDocVaXuatDocLap` | Che mật khẩu/token/số tài khoản; quyền audit độc lập. |

Test UI chung: `QuanLy/DungChung/OperationsUiTests.cs`, mở WinForms thật với API/SQL, chụp desktop/compact, kiểm tra wrap nút và chiều cao báo cáo; ADM-06 kiểm tra thêm/bỏ dòng và lưu nháp qua form thật.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-10"
```

Database GUID riêng trong Docker; không chạy reset/seed trên database quán. Test không thay thế kiểm thử máy in thật và sao kê ngân hàng thực tế.

## Điều hướng trang liên quan

`ReportNavigationUiTests.SauTab_ChuyenDungTrang_KhongCanChonDong`: chạy WinForms thật, lần lượt chọn sáu tab; kiểm tên nút, bỏ chọn toàn bộ dòng rồi bấm để mở đúng trang Thực đơn/Đơn hàng/Tồn kho/Hoàn tiền trong cùng MainForm. Không mở popup chi tiết cũ, không ghi dữ liệu nghiệp vụ.
