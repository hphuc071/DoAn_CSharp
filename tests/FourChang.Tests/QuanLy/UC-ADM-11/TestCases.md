# UC-ADM-11 — Test case

Nguồn: docs/use-cases/UC-ADM-11.md. BR: 05,06,08,09,13,14,28,29,33. Tất cả dùng chung FourChang.Tests.csproj, dữ liệu fixture riêng.

| Nhóm | Luồng / BR | File, phương thức | Kết quả cần đạt |
|---|---|---|---|
| Thêm, tìm, phân trang, tên trùng | Chính, A1, E2/E6; BR13,14,29,33 | EmployeeAdministrationTests.Them_Tim_PhanTrang_TenTrung_VaChongLap | Tên trùng có mã riêng; lặp cùng mã chỉ một hồ sơ; trang rỗng hợp lệ |
| Sửa/trạng thái | A2/A3, E3; BR08,09,28,29,33 | Sua_HoSo_TrangThai_VaVersion | Địa chỉ lưu đúng, stale version409, có lý do, không sinh tài khoản |
| Ca đang mở/lịch sử | A3/A4/A6, E4; BR09,33 | CoCa_ChanNgung_Xoa_VaGiuSnapshot | Chặn ngừng/xóa, lịch sử giữ tên đã chốt |
| Xóa/rollback | A6, E7; BR14,29,33 | XoaCoDieuKien_VaRollbackAudit | Chỉ xóa hồ sơ chưa tham chiếu, receipt lặp; audit lỗi rollback |
| Dữ liệu/quyền | E1/E5; BR05,06,08,33 | DuLieuSai_VaPhanQuyen | Dữ liệu sai400, ẩn danh401, Staff/Customer403 |
| UI hồ sơ | Chính, A1/A2/A4/A5 | EmployeeUiTests.HoSo_TimKiem_DiaChi_LichSu_ThaoTacBangControl | Nhập bằng control thật, địa chỉ lưu, hủy không ghi, tìm/tải lại, lịch sử đúng nhân viên |
| Cạnh tranh mở/ngừng và người nhận | E3/E4; BR09,29,33 | UC-ADM-12/WorkSessionConcurrencyTests | Không có nhân viên INACTIVE mở ca mới; chặn người đang chờ bàn giao |

Chạy: `dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-11"`. API/SQL thật; WinForms chạy trên Windows STA. Ảnh/geometry trong artifacts/tests; không chứa mã phục hồi/token.
