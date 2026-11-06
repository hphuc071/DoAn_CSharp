# Kiểm thử UC ADM 12

| ID | Tình huống | Kết quả | Cách kiểm |
|---|---|---|---|
| 12-01 | Thiếu tên/ngày, điện thoại sai, thiếu GUID | 400, không lưu | EmployeeAdministrationTests |
| 12-02 | Tạo hồ sơ ACTIVE, gửi cạnh tranh cùng mã | Một hồ sơ, cùng receipt | EmployeeAdministrationTests |
| 12-03 | Cùng mã khác nội dung | 409 | EmployeeAdministrationTests |
| 12-04 | Hai người cùng họ tên/số điện thoại | Hai mã nội bộ khác nhau | EmployeeAdministrationTests |
| 12-05 | Tìm tên tiếng Việt không dấu, phân trang, lọc sai, rỗng | Đúng tổng/trang, 400 hoặc rỗng hợp lệ | EmployeeAdministrationTests |
| 12-06 | Sửa bản mới / phiên bản cũ | 200 / 409 | EmployeeAdministrationTests |
| 12-07 | Ngừng khi còn ca; ngừng sau chốt; kích hoạt lại | Chặn ca mở, giữ lịch sử; không hồi sinh ca cũ | EmployeeAdministrationTests |
| 12-08 | Customer/Staff/ẩn danh dùng Admin | 403/403/401 | EmployeeAdministrationTests |
| 12-09 | Không ghi được audit | Rollback hồ sơ | EmployeeAdministrationTests |
| 12-10 | Xóa từ khóa rồi Tải lại | Về tổng ban đầu | WorkforceUiTests |
| 12-11 | Hủy form trước lưu; form thiếu dữ liệu giữ nháp | Không ghi; lỗi rõ | Thủ công UI |
| 12-12 | Lịch sử ca từ hồ sơ | ADM-13 lọc đúng MaNhanVien, quay lại được | Thủ công UI + test UI bộ lọc |
| 12-13 | Mất phản hồi sau lưu | Tra receipt bằng GUID, không tạo mã mới | MatPhanHoiSauCommit_VaOffline_KhongTaoTrung (proxy mất phản hồi thật) |

Các test tự động chạy SQL thật trong database GUID riêng qua một project FourChang.Tests.csproj. Không xóa dữ liệu thật.
