# DoAn_CSharp
#Trương Quốc Thái : 3124411271
#Nguyễn Đức Tài : 3124411262
#Nguyễn Hoàng Phúc : 3124411237
#Trần Văn Thiện: 3124411288

## **1\. Bảng phân công làm Code và Thành phần dùng chung (21 UC & 30 Bảng CSDL)** 

### **1.1. Bảng phân công Use Case và Thư mục mã nguồn**

| Thành viên | Phân hệ | Mã UC phụ trách | Số lượng | Phạm vi nghiệp vụ & Thư mục code chính |
| :---- | :---- | :---- | :---- | :---- |
| **Tài** | Khách hàng (Website) | UC-WEB-01 đến UC-WEB-04 | 4 UC | Đăng ký, đăng nhập/đăng xuất, hồ sơ & sổ địa chỉ, duyệt/lọc thực đơn (Pages/Account, Pages/Menu, Security, Features/Customer/Account, Features/Customer/Menu) |
| **Thái** | Khách hàng (Website) | UC-WEB-05 đến UC-WEB-08 | 4 UC | Tùy chỉnh món & giỏ hàng, đặt món đa hình thức (tại bàn/mang đi/giao hàng), thanh toán VietQR mô phỏng, quản lý đơn cá nhân & yêu cầu hủy (Pages/Cart, Pages/Checkout, Pages/Orders, Features/Customer/Orders) |
| **Phúc** | Nhân viên (Desktop POS) | UC-POS-01 đến UC-POS-05 | 5 UC | Mở/kết thúc ca theo ngày UTC+07, tạo đơn tại quầy, tiếp nhận & xử lý pha chế trừ kho, yêu cầu hủy món trước pha chế, thanh toán & phát hành hóa đơn/chi hoàn tiền mặt (FourChang.Pos, Desktop/Staff, Features/Staff) |
| **Thiện** | Quản lý (Desktop Admin) | UC-ADM-01 đến UC-ADM-08 | 8 UC | Quản lý khách hàng (ADM-01), thực đơn & công thức (ADM-02), nguyên liệu g/ml (ADM-03), quản lý kho (ADM-04), đơn hàng & duyệt hủy (ADM-05), hóa đơn & duyệt hoàn tiền (ADM-06), báo cáo & nhật ký (ADM-07), nhân viên & ca làm việc (ADM-08) (FourChang.Admin, Desktop/Admin, Features/Admin) |

### **1.2. Bảng phân công Đầu mối các Thành phần dùng chung**

| Thành phần dùng chung | Đầu mối chính | Phối hợp | Bảng dữ liệu & Thư mục liên quan |
| :---- | :---- | :---- | :---- |
| Xác thực, phiên đăng nhập & phân quyền | **Thiện** | Tài, Thái, Phúc | TaiKhoanKhachHang, TaiKhoanQuanLy, PhienDangNhap (Api/Shared/Authentication, Api/Security) |
| Thực đơn, cấu hình món, topping & công thức | **Thiện** | Tài, Thái, Phúc | Mon, CauHinhMon, MonThem, CongThuc, ChiTietCongThuc, Banner (Domain/Entities/Menu) |
| Nguyên liệu, giữ chỗ, trừ kho & biến động kho | **Thiện** | Tài, Thái, Phúc | NguyenLieu, TonKho, PhieuKho, ChiTietPhieuKho, BienDongKho, NguyenLieuChiTietDonHang |
| Duyệt hủy, hoàn tiền & Audit Log | **Thiện** | Tài, Thái, Phúc | HuyMon, HoSoHoanTien, LanChiHoanTien, NhatKyHeThong (Api/Shared/Audit) |
| Tính giá đơn hàng & phí giao hàng (15.000đ) | **Thái & Tài** | Phúc, Thiện | DonHang, ChiTietDonHang, MonThemChiTietDonHang (Features/Customer/Orders) |
| Thanh toán VietQR & giao dịch mô phỏng | **Thái** | Tài, Phúc, Thiện | ThanhToan, GiaoDichNganHang (Domain/Payments, Features/Customer/Orders) |
| Tạo đơn, chuyển trạng thái & đồng bộ sự kiện | **Phúc** | Tài, Thái, Thiện | Ban, DonHang, SuKienDongBo (Domain/Entities/Orders, Presentation.Shared) |
| Phát hành hóa đơn & đối chiếu ca làm việc | **Phúc** | Tài, Thái, Thiện | NhanVien, CaLamViec, HoaDon (Desktop/Staff/Invoices, Desktop/Staff/WorkSessions) |

## **2\. Bảng phân công viết Bài báo cáo (Word) và Từ điển dữ liệu (Excel 30 Bảng)**

| Thành viên | Chương 1 & Chương 2 (Word) | Chương 3: Phân tích & Thiết kế (Word) | Chương 4 & File Excel (30 bảng CSDL) |
| :---- | :---- | :---- | :---- |
| **Thiện** | Mục 1.1 (Khảo sát), Mục 1.4 (Kế hoạch); Công nghệ C\#, Web API, EF Core, SQL Server | Sơ đồ ngữ cảnh, Kiến trúc phân lớp, Lược đồ 3NF & Vật lý, Đặc tả & Activity UC-ADM-01 đến UC-ADM-04 | Tổng hợp Word, Mục lục & Danh mục hình/bảng; Từ điển & Ràng buộc 13 bảng (TaiKhoanQuanLy, PhienDangNhap, Mon, CauHinhMon, MonThem, CongThuc, ChiTietCongThuc, Banner, NguyenLieu, PhieuKho, ChiTietPhieuKho, TonKho, BienDongKho, NhatKyHeThong) |
| **Phúc** | Mục 1.2.1 (Quy trình nghiệp vụ POS, pha chế & chốt ca); Công nghệ Windows Forms Desktop | Sơ đồ Use Case POS, Swimlane Workflow xử lý đơn & trừ kho, Đặc tả & Activity UC-POS-01 đến UC-POS-05 | Chụp màn hình & kiểm thử POS; Từ điển & Ràng buộc 5 bảng (NhanVien, CaLamViec, Ban, HoaDon, SuKienDongBo) |
| **Tài** | Mục 1.2.2 (Tác nhân), Mục 1.2.3 (Chức năng); Công nghệ ASP.NET Core Razor Pages & Responsive | Sơ đồ Use Case tổng quát (21 UC), Conceptual ERD, Đặc tả & Activity UC-WEB-01 đến UC-WEB-04 | Chụp màn hình & kiểm thử Web (Tài khoản, Menu); Từ điển & Ràng buộc 2 bảng (TaiKhoanKhachHang, DiaChiKhachHang) |
| **Thái** | Mục 1.3 (Yêu cầu nghiệp vụ & 39 Quy tắc nghiệp vụ BR); Công nghệ VietQR & xuất báo cáo Excel/PDF | Swimlane Workflow Đặt hàng VietQR & Hủy/Hoàn tiền, Đặc tả & Activity UC-WEB-05 đến UC-WEB-08 và UC-ADM-05 đến UC-ADM-08 | Chụp màn hình & kiểm thử Giỏ, Đặt đơn, VietQR, Admin Đơn/Hoàn/Báo cáo; Từ điển & Ràng buộc 9 bảng (DonHang, ChiTietDonHang, MonThemChiTietDonHang, NguyenLieuChiTietDonHang, HuyMon, ThanhToan, GiaoDichNganHang, HoSoHoanTien, LanChiHoanTien) |

## 

## **3\. Kế hoạch thực hiện Đồ án (Lộ trình 12 Tuần & 7 Chặng tích hợp)**

| Giai đoạn | Thời gian | Chặng tích hợp kỹ thuật | Sản phẩm bàn giao trọng tâm |
| :---- | :---- | :---- | :---- |
| **Giai đoạn 1** | Tuần 1 – Tuần 4 | Chuẩn hóa 21 UC, 39 BR & Schema 30 bảng | Báo cáo Đăng ký đề tài (Chương 1, 2, 3), Đặc tả 21 UC, File Excel Từ điển dữ liệu 30 bảng |
| **Giai đoạn 2** | Tuần 5 – Tuần 9 | Chặng 1, 2, 3 & 4 (Tài khoản, Ca, Menu, Kho, Đặt đơn, VietQR) | Báo cáo Giữa kỳ (60–70% code): Chạy thông suốt Web \+ Desktop \+ SQL Server |
| **Giai đoạn 3** | Tuần 10 – Tuần 11 | Chặng 5, 6 & 7 (Xử lý pha chế, Duyệt hủy/Hoàn tiền, Hóa đơn, Báo cáo) | Báo cáo Cuối kỳ hoàn chỉnh, 100% Source Code \+ Bộ kiểm thử tự động xUnit |
| **Giai đoạn 4** | Tuần 12 | Kiểm thử toàn diện 9 luồng tích hợp liên phân hệ | Demo trực tiếp Web – POS – Admin và Vấn đáp cá nhân |

### 

### **Lộ trình chi tiết theo từng tuần**

* **Tuần 1 – Tuần 2 (Phân tích nghiệp vụ & Đặc tả 21 Use Case):**  
  * Rà soát và đồng bộ toàn bộ 21 Use Case (8 WEB, 5 POS, 8 ADM) cùng hệ thống quy tắc nghiệp vụ (BR-01 đến BR-39), giữ nguyên 15.000đ phí giao hàng mô phỏng, tách bạch duyệt hủy (HuyMon) và chi hoàn tiền mặt tại quầy (HoSoHoanTien, LanChiHoanTien).  
  * Hoàn thành Chương 1, Chương 2 và các sơ đồ Use Case, Swimlane Workflow trong Chương 3\.  
* **Tuần 3 – Tuần 4 (Thiết kế CSDL 30 bảng & Mốc Báo cáo Đăng ký đề tài):**  
  * **Thiện** chốt lược đồ CSDL 30 bảng chuẩn 3NF trên SQL Server (bỏ bảng thiết bị POS và hợp nhất giữ chỗ nguyên liệu vào NguyenLieuChiTietDonHang).  
  * Cả nhóm điền đầy đủ Từ điển dữ liệu và Ràng buộc toàn vẹn cho 30 bảng trên Google Sheets/Excel, hoàn thành Báo cáo Giai đoạn 1 để nộp và trình bày tại Tuần 4\.  
* **Tuần 5 — Chặng 1 (Tài khoản, Phân quyền & Ca làm việc):**  
  * **Thiện** xây dựng xác thực/phân quyền (PhienDangNhap), UC-ADM-01 (Quản lý khách hàng) và UC-ADM-08 (Quản lý nhân viên & ca làm việc).  
  * **Tài** hoàn thiện UC-WEB-01, 02, 03 (Đăng ký, đăng nhập/đổi mật khẩu tạm, hồ sơ & sổ địa chỉ).  
  * **Phúc** hoàn thiện UC-POS-01 (Mở ca bằng mã & họ tên nhân viên ACTIVE theo ngày UTC+07, kiểm đếm và kết thúc ca).  
* **Tuần 6 — Chặng 2 (Thực đơn, Công thức BOM, Nguyên liệu & Kho):**  
  * **Thiện** hoàn thiện UC-ADM-02 (Thực đơn, cấu hình size/nhiệt độ, topping & công thức), UC-ADM-03 (Nguyên liệu g/ml) và UC-ADM-04 (Quản lý kho: nhập, xuất hỏng, kiểm kê, ghi sổ/hủy hiệu lực phiếu).  
  * **Tài** hoàn thiện UC-WEB-04 (Duyệt, tìm kiếm, lọc thực đơn theo trạng thái khả dụng) và viết bộ test xUnit cho UC-WEB-01 đến UC-WEB-04.  
* **Tuần 7 — Chặng 3 (Giỏ hàng, Tạo đơn đa hình thức & Giữ chỗ kho):**  
  * **Thái** xây dựng UC-WEB-05 (Tùy chỉnh món & giỏ hàng) và UC-WEB-06 (Đặt món tại bàn, mang đi, giao tận nơi phí 15.000đ, lưu snapshot và giữ chỗ nguyên liệu trong NguyenLieuChiTietDonHang).  
  * **Phúc** xây dựng UC-POS-02 (Tạo đơn tại quầy thu tiền mặt hoặc chuyển khoản).  
* **Tuần 8 – Tuần 9 — Chặng 4 (Thanh toán VietQR & Mốc Báo cáo Giữa kỳ):**  
  * **Thái** hoàn thiện UC-WEB-07 (Thanh toán VietQR mô phỏng, chuyển đơn sang PENDING\_CONFIRMATION và dọn checkout cho đơn tiếp theo); **Phúc** tích hợp thu tiền mặt/QR tại quầy (UC-POS-05).  
  * **Tuần 9:** Demo trực tiếp luồng liên thông giữa Admin (nhập kho, chỉnh thực đơn), Web (đặt món & thanh toán VietQR) và POS (mở ca & nhận đơn).  
* **Tuần 10 – Tuần 11 — Chặng 5, 6, 7 (Xử lý đơn, Duyệt hủy/Hoàn tiền, Hóa đơn, Báo cáo & Nộp Cuối kỳ):**  
  * **Phúc** hoàn thiện UC-POS-03 (Tiếp nhận, bắt đầu pha trừ kho thực tế 1 lần, pha xong, bàn giao/giao hàng), UC-POS-04 (Gửi/chuyển yêu cầu hủy món chưa pha) và UC-POS-05 (Phát hành hóa đơn bất biến, chi hoàn tiền mặt tại quầy).  
  * **Thái** hoàn thiện UC-WEB-08 (Theo dõi tiến độ đơn, gửi yêu cầu hủy đơn chờ xác nhận, xem hướng dẫn đến quầy nhận hoàn) và viết bộ test xUnit cho UC-WEB-05 đến UC-WEB-08.  
  * **Thiện** hoàn thiện UC-ADM-05 (Giám sát đơn & duyệt/từ chối hủy), UC-ADM-06 (Tra cứu hóa đơn & duyệt hạn mức hoàn tiền tại quầy) và UC-ADM-07 (Thống kê doanh thu theo ngày hoàn thành, xuất Excel & tra cứu NhatKyHeThong).  
  * **Tuần 11:** Chạy kiểm thử tích hợp toàn diện giữa 3 phân hệ, chốt cuốn Báo cáo cuối kỳ kèm File Excel Từ điển dữ liệu 30 bảng và Source Code.  
* **Tuần 12 (Bảo vệ & Vấn đáp Đề tài):**  
  * Demo trọn vẹn kịch bản liên hoàn 3 màn hình (Admin – Web – POS) và vấn đáp cá nhân theo đúng các Use Case và bảng dữ liệu từng thành viên phụ trách.

## Chạy đồ án local

Mở Docker Desktop. Trong thư mục dự án, chuẩn bị database rồi mở cấu hình đồ án:

```powershell
.\ChuanBi-Database.ps1
.\scripts\Enable-LocalDemo.ps1
```

Sau đó chạy `Chay-Backend.cmd`, `Chay-Web.cmd` và launcher Quản lý/Nhân viên tương ứng. Website mặc định http://localhost:15880; URL API lấy từ `.env`. Sau khi đổi cấu hình cần khởi động lại API/Web. Khách đăng nhập trước khi đặt món.

Chế độ local mở tạo đơn Web, mang đi/tại bàn/giao hàng15.000đ, QR thanh toán mô phỏng và yêu cầu hủy theo quy trình duyệt. QR được ghi nhãn mô phỏng; không xác minh giao dịch ngân hàng thật. `.env` và mật khẩu không đưa lên Git. Các giá trị mặc định trong `.env.example` giữ tắt thao tác ghi cho tới khi chủ động chạy lệnh mở local.
Tuần 1-3: Tìm hiểu, thống nhất các use case và công nghệ.
Tuần 4: Thiết kế database và design mẫu.
Tuần 5-7: Tiến hành làm giao diện và các chức năng.
