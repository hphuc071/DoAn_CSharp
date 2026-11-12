# UC-ADM-12 — Test case

Nguồn: docs/use-cases/UC-ADM-12.md. BR: 05,06,08,09,14,19,29,33,36. Test dùng API/SQL thật, database fixture riêng và cùng project xUnit.

| Nhóm | Luồng / BR | File / phương thức | Kết quả cần đạt |
|---|---|---|---|
| Xem, gửi đếm, chốt | Chính, A1/A3/A6, E2/E3/E6; BR08,09,19,29,36 | WorkSessionAdministrationTests.GuiDoiChieu_ChotCa_Quyen_VaChongLap | NULL khác0, Admin không đếm thay, nhân viên gửiLOCKED, chốt một lần |
| Nguồn tiền | Chính4/5, A3, E2/E4; BR19,29,36 | NguonTien_ThayDoi_BuocDoiChieuLai | CASH trừ hoàn thành công, CK riêng; nguồn đổi cùng tổng vẫn409 |
| Bàn giao | A2, E4/E5; BR09,29,33,36 | BanGiao_DonDo_KhoanChuaRo_GiuLichSu_VaNhanCa | Kiểm đủ đơn/khoản; chủ nguồn giữ nguyên; người nhận ACTIVE, ca mới liên kết ca trước |
| Mất phiên | A4, E5/E6; bổ sung đã duyệt | MatPhien_MaPhucHoiMotLan_ChiDoiChieu | Mã dùng một lần; audit không chứa mã rõ; không bán hoặc tự chốt |
| Khóa tạm | A4/A6; BR08,09,36 | KhoaTam_KiemPhien_TruocKhiAdminChoTiepTuc | Cần yêu cầu từ phiên đúng; không mở ca chờ đếm như khóa tạm |
| Audit thất bại | E8; BR14,29 | ChotCa_AuditHong_Rollback | Không chốt hoặc ghi nhật ký một phần |
| Race mở/ngừng | E3/E5; BR09,29,33 | WorkSessionConcurrencyTests.MoCa_DuaVoiNgungNhanVien_KhongDeHoSoNgungCoCaMoi | Hai lệnh tuần tự hóa, không mở ca cho hồ sơ đã ngừng |
| Race hai bàn giao | E5; BR09,29,36 | HaiCa_BanGiaoCungNguoiNhan_ChiMotBenThanhCong | Chỉ một ca giành người/máy nhận, bên còn lại giữ nguyên |
| Hết hạn/cấp lại | A4, E6; bổ sung đã duyệt | MaPhucHoi_CapLai_VaHetHan_KhongTaiSuDung | Mã và phiên cũ vô hiệu; hết hạn bị từ chối |
| Thiếu/0 và mã máy | A4/A6, E3 | KhongPhucHoiBangMaMay_ThucDemKhongKhacThieu | Không lấy quyền từ máy/tên; nhập0 hợp lệ, thiếu bị400 |
| UI end-to-end | Chính, A1/A4/A6 | WorkSessionUiTests.DoiChieu_PhucHoiMotLan_POS_VaTrangThaiChot | Admin cấp mã bằng dialog → POS đổi mã → gửi đếm → Admin chốt; chặn màn hình bán; kiểm footer, viewport và chiều cao thẻ |

Chạy: `dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-12"`. Ảnh WinForms và geometry lưu artifacts/tests; không chụp dialog chứa mã bí mật. Test mạng/receipt kiểm thử replay và kết quả chưa rõ qua API; không khẳng định đã mô phỏng mọi dạng sự cố mạng ngoài thực tế.
