# UC-ADM-07 — Giám sát và xử lý đơn hàng

Nguồn: `dac-25-Uc.docx`, giữ nguyên đánh số 25 UC. Quy tắc: BR-02, BR-04, BR-05, BR-08, BR-13, BR-14, BR-15, BR-16, BR-17, BR-21, BR-22, BR-26, BR-29, BR-33, BR-34, BR-36.

| Test xUnit | Kết quả cần bảo đảm |
|---|---|
| `VongDoi_TruKhoLucBatDau_KhongTruLaiLucHoanTat` | Bắt đầu pha tiêu hao một lần; không hủy món đã pha. |
| `HuyMotPhan_GiaiPhongGiuCho_TaoHoSoChoHoan` | Hủy trước pha giải phóng giữ chỗ, tạo REQUESTED; chưa ghi chi. |
| `ChuaThuDu_KhongHoanTat_VersionCuKhongGhiDe` | Chưa trả đủ chặn bàn giao; stale version 409. |
| `GiaoThatBai_CoChungCu_GiaoLai_KhongHoiKho` | Giao lại có xác nhận khi chưa chốt thất bại; DELIVERY_FAILED kết thúc ngoại lệ, không hoàn kho. |

Test UI chung: `QuanLy/DungChung/OperationsUiTests.cs`, mở WinForms thật với API/SQL, chụp desktop/compact, kiểm tra wrap nút và chiều cao báo cáo; ADM-06 kiểm tra thêm/bỏ dòng và lưu nháp qua form thật.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-07"
```

Database GUID riêng trong Docker; không chạy reset/seed trên database quán. Test không thay thế kiểm thử máy in thật và sao kê ngân hàng thực tế.
