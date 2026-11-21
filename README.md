# DoAn_CSharp
#Trương Quốc Thái : 3124411271
#Nguyễn Đức Tài : 3124411262
#Nguyễn Hoàng Phúc : 3124411237
#Trần Văn Thiện: 3124411288

## **1\. Bảng phân công làm Code và Thành phần dùng chung**

Hệ thống giữ nguyên 35 bảng nghiệp vụ trên SQL Server và 25 Use Case (8 UC Khách hàng Web, 5 UC Nhân viên POS, 12 UC Quản lý Admin sau khi lược bỏ UC quản lý thiết bị POS riêng theo quy tắc BR-08).

### **1.1. Bảng phân công Use Case theo thành viên**

| Thành viên | Phân hệ | Mã UC phụ trách | Số lượng | Phạm vi nghiệp vụ chính |
| :---- | :---- | :---- | :---- | :---- |
| **Tài** | Khách hàng (Website) | UC-WEB-01 đến UC-WEB-04 | 4 UC | Đăng ký, đăng nhập, hồ sơ, thực đơn |
| **Thái** | Khách hàng (Website) | UC-WEB-05 đến UC-WEB-08 | 4 UC | Giỏ hàng, đặt món, VietQR, đơn cá nhân |
| **Phúc** | Nhân viên (Desktop POS) | UC-POS-01 đến UC-POS-05 | 5 UC | Mở/chốt ca, đơn quầy, pha chế, hóa đơn |
| **Thiện** | Quản lý (Desktop Admin) | UC-ADM-01 đến UC-ADM-08 | 8 UC | Khách, nhân viên, ca, menu, kho, hoàn tiền, báo cáo |

**Chi tiết công việc lập trình và kiểm thử của từng thành viên:**

* **Tài (Phân hệ Khách hàng — UC-WEB-01 đến UC-WEB-04):**  
  * Xây dựng giao diện Razor Pages và API cho UC-WEB-01 (Đăng ký tài khoản), UC-WEB-02 (Đăng nhập, đăng xuất, đổi mật khẩu tạm) và UC-WEB-03 (Hồ sơ cá nhân, sổ địa chỉ) tại thư mục Account và Security.  
  * Xây dựng UC-WEB-04 (Xem trang chủ, banner, món bán chạy; tìm kiếm tiếng Việt không dấu, lọc theo danh mục/khoảng giá và hiển thị trạng thái còn/hết theo tồn kho khả dụng) tại thư mục Menu.  
  * Viết kiểm thử tự động C\# xUnit và tài liệu TestCases cho 4 Use Case từ UC-WEB-01 đến UC-WEB-04 (đã hoàn thành 8 kịch bản kiểm thử tự động).  
* **Thái (Phân hệ Khách hàng — UC-WEB-05 đến UC-WEB-08):**  
  * Xây dựng UC-WEB-05 (Tùy chỉnh size, nóng/lạnh, đường, đá, topping và quản lý giỏ hàng) cùng UC-WEB-06 (Đặt món QR bàn, mang đi, giao tận nơi kèm giữ chỗ nguyên liệu BOM) tại thư mục Cart và Checkout.  
  * Xây dựng UC-WEB-07 (Thanh toán chuyển khoản VietQR, đối soát tự động, xử lý hết hạn chờ thanh toán) và UC-WEB-08 (Theo dõi đơn thời gian thực, yêu cầu hủy đơn chờ xác nhận) tại thư mục Checkout và Orders.  
  * Viết kiểm thử tự động C\# xUnit và tài liệu TestCases cho 4 Use Case từ UC-WEB-05 đến UC-WEB-08 trong thư mục kiểm thử Khách hàng.  
* **Phúc (Phân hệ Nhân viên POS — UC-POS-01 đến UC-POS-05):**  
  * Thực hiện UC-POS-01 (Mở ca bằng đối chiếu mã và họ tên nhân viên đang hoạt động, tạm khóa/khôi phục ca, gửi đối chiếu hoặc kết thúc ca) tại thư mục Shell và WorkSessions.  
  * Thực hiện UC-POS-02 (Tạo đơn tại quầy), UC-POS-03 (Tiếp nhận đơn, bắt đầu pha chế trừ tồn thực tế 1 lần và giảm giữ chỗ nguyên tử) và UC-POS-04 (Hủy món chưa pha chế kèm lý do) tại thư mục Sales và Orders.  
  * Thực hiện UC-POS-05 (Thu tiền mặt hoặc VietQR tại quầy, phát hành hóa đơn bất biến, in bản sao và xuất PDF) kèm bộ kiểm thử xUnit cho phân hệ Nhân viên tại thư mục Invoices.  
* **Thiện (Phân hệ Quản lý Admin — UC-ADM-01 đến UC-ADM-08):**  
  * Thực hiện nhóm quản trị tài khoản và nhân sự gồm UC-ADM-01 (Tài khoản và sổ địa chỉ khách hàng), UC-ADM-07(Hồ sơ nhân viên, giám sát, đối chiếu, chốt và bàn giao ca làm việc).  
  * Thực hiện nhóm thực đơn và kho gồm UC-ADM-02 (Thực đơn, biến thể, topping, banner), UC-ADM-03 (Nguyên liệu và quy đổi), UC-ADM-04 (Công thức BOM), UC-ADM-05 (Tồn kho và cảnh báo) và UC-ADM-06 (Chứng từ kho).  
  * Thực hiện nhóm vận hành và báo cáo gồm UC-ADM-07 (Giám sát đơn và xử lý ngoại lệ), UC-ADM-08 (Quản lý và điều chỉnh hóa đơn), UC-ADM-09 (Duyệt và xác minh hoàn tiền) và UC-ADM-10 (Báo cáo Excel/PDF và Nhật ký hệ thống).

### **1.2. Bảng phân công Đầu mối các Thành phần dùng chung**

| Thành phần dùng chung | Làm chính | Phối hợp | Thư mục triển khai |
| :---- | :---- | :---- | :---- |
| Xác thực, phiên và phân quyền | **Thiện** | Tài, Thái, Phúc | Api/Shared/Authentication và Security |
| Thực đơn, cấu hình món và giá | **Thiện** | Tài, Thái, Phúc | Domain/Entities/Menu và Infrastructure |
| Giữ chỗ, trừ kho và khả dụng | **Thiện** | Tài, Thái, Phúc | Domain/Entities/Inventory và Shared/Data |
| Xử lý hoàn tiền và Audit Log | **Thiện** | Tài, Thái, Phúc | Api/Shared/Audit và Domain/Entities/Audit |
| Tính tổng tiền và phí giao hàng | **Thái và Tài** | Phúc, Thiện | Features/Customer/Orders và Presentation.Shared |
| VietQR và đối soát chuyển khoản | **Thái** | Tài, Phúc, Thiện | Features/Customer/Orders và Domain/Payments |
| Tạo đơn và chuyển trạng thái | **Phúc** | Tài, Thái, Thiện | Domain/Entities/Orders và Presentation.Shared |
| Phát hành hóa đơn và đồng bộ real-time | **Phúc** | Tài, Thái, Thiện | Desktop/Staff/Invoices và Api/Shared |

* **Cách phối hợp phần dùng chung giữa Tài và Thái:** Tài kết nối trực tiếp với dịch vụ xác thực (UC-WEB-01 đến 03\) và dịch vụ thực đơn/khả dụng kho của Thiện (UC-WEB-04), đồng thời hỗ trợ Thái xây dựng logic tính giá. Thái làm đầu mối chính cho dịch vụ tính tổng tiền/phí giao hàng (UC-WEB-05, 06\) và đối soát chuyển khoản VietQR (UC-WEB-07).

## **2\. Bảng phân công viết Bài báo cáo (Word) và Tài liệu dữ liệu (Excel)**

Cấu trúc bài báo cáo tuân thủ đủ 9 mục bắt buộc trong đề cương của TS. Đỗ Như Tài và phương pháp trình bày của các tài liệu mẫu (HSURVEY, MicroShop, Coolstore).

| Thành viên | Chương 1 và Chương 2 (Word) | Chương 3: Phân tích và Thiết kế (Word) | Chương 4 và File Excel (35 bảng) |
| :---- | :---- | :---- | :---- |
| **Thiện** | Mục 1.1, 1.4; Công nghệ C\#, EF Core, SQL Server | Sơ đồ ngữ cảnh, Kiến trúc, Lược đồ 3NF, UC-ADM-01..06 | Tổng hợp Word; Từ điển bảng Tài khoản, Menu, BOM, Kho |
| **Phúc** | Mục 1.2.1; Công nghệ Windows Forms Desktop | Swimlane POS & Trừ kho, Đặc tả và Sơ đồ UC-POS-01..05 | Màn hình POS; Từ điển bảng Nhân viên, Ca, Hóa đơn |
| **Tài** | Mục 1.2.2, 1.2.3; Công nghệ ASP.NET Core, Responsive | Sơ đồ UC tổng quát, Conceptual ERD, Đặc tả UC-WEB-01..04 | Màn hình Web (Tài khoản, Menu); Từ điển bảng Khách hàng |
| **Thái** | Mục 1.3 (36 BR); Công nghệ VietQR, Báo cáo Excel/PDF | Swimlane Đặt đơn VietQR & Hoàn tiền, UC-WEB-05..08, ADM-07..12 | Màn hình Giỏ, Đặt đơn, VietQR, Admin Đơn/Báo cáo; Bảng Đơn, Thu/Hoàn |

**Chi tiết đầu việc báo cáo của từng thành viên:**

* **Phần việc báo cáo của Tài (Đồng bộ với UC-WEB-01 đến UC-WEB-04):**  
  * **Chương 1 và Chương 2:** Viết Mục 1.2.2 (Xác định các tác nhân liên quan), Mục 1.2.3 (Tổng hợp các chức năng hệ thống) và khảo sát công nghệ ASP.NET Core Razor Pages cùng thiết kế giao diện chuẩn Responsive.  
  * **Chương 3:** Vẽ Sơ đồ Use Case tổng quát (25 UC), Sơ đồ Use Case phân rã Khách hàng, Lược đồ ERD mức ý niệm; viết đặc tả chi tiết, User Stories (*Given – When – Then*) và vẽ Activity Diagram cho UC-WEB-01 đến UC-WEB-04.  
  * **Chương 4 và File Excel:** Chụp màn hình thực tế và kết quả kiểm thử của UC-WEB-01 đến UC-WEB-04; điền Từ điển dữ liệu và Ràng buộc toàn vẹn cho các bảng Tài khoản khách hàng, Hồ sơ khách hàng, Phiên đăng nhập và Sổ địa chỉ.  
* **Phần việc báo cáo của Thái (Đồng bộ với UC-WEB-05 đến UC-WEB-08 và hỗ trợ phần Admin):**  
  * **Chương 1 và Chương 2:** Viết Mục 1.3 (Yêu cầu chức năng, phi chức năng và chuẩn hóa bảng 36 Quy tắc nghiệp vụ từ BR-01 đến BR-36) cùng phần khảo sát công nghệ tích hợp VietQR và xuất báo cáo Excel/PDF.  
  * **Chương 3:** Vẽ Swimlane Workflow cho quy trình Đặt món VietQR và Hủy đơn/Hoàn tiền; trình bày đặc tả, User Stories và vẽ Activity Diagram cho UC-WEB-05 đến UC-WEB-08 cùng nhóm UC-ADM-07 đến UC-ADM-12.  
  * **Chương 4 và File Excel:** Chụp màn hình Giỏ hàng, Đặt món, Thanh toán VietQR, Quản lý đơn cá nhân; điền Từ điển dữ liệu và Ràng buộc toàn vẹn cho các bảng Giỏ hàng, Đơn hàng, Dòng đơn, Topping dòng đơn, Giao dịch thu và Hoàn tiền.  
* **Phần việc báo cáo của Phúc (Đồng bộ với UC-POS-01 đến UC-POS-05):**  
  * **Chương 1 và Chương 2:** Viết Mục 1.2.1 (Mô tả quy trình nghiệp vụ tổng thể tại quầy POS, pha chế và đối chiếu ca làm việc) và khảo sát công nghệ Windows Forms Desktop.  
  * **Chương 3:** Vẽ Sơ đồ Use Case phân rã POS, Swimlane Workflow xử lý đơn và tự động trừ kho BOM; trình bày đặc tả, User Stories và Activity Diagram cho UC-POS-01 đến UC-POS-05.  
  * **Chương 4 và File Excel:** Chụp màn hình thực tế phân hệ Nhân viên POS; điền Từ điển dữ liệu và Ràng buộc toàn vẹn cho các bảng Hồ sơ nhân viên, Ca làm việc, Thiết bị POS lịch sử và Hóa đơn.  
* **Phần việc báo cáo của Thiện (Đồng bộ với UC-ADM-01 đến UC-ADM-12 và Tổng hợp):**  
  * **Chương 1 và Chương 2:** Viết Mục 1.1 (Khảo sát hiện trạng và phần mềm liên quan), Mục 1.4 (Kế hoạch thực hiện) và khảo sát công nghệ C\#, ASP.NET Core Web API, Entity Framework Core cùng Microsoft SQL Server.  
  * **Chương 3:** Vẽ Sơ đồ ngữ cảnh (Business Context), Sơ đồ Kiến trúc phân lớp, Lược đồ luận lý chuẩn 3NF và Database Diagram vật lý của 35 bảng; trình bày đặc tả và Activity Diagram cho UC-ADM-01 đến UC-ADM-06.  
  * **Chương 4 và File Excel:** Tạo khung Google Sheets 4 Sheet, điền Từ điển dữ liệu nhóm bảng Thực đơn, Biến thể, Topping, Nguyên liệu, BOM, Tồn kho, Chứng từ kho, Nhật ký hệ thống và tổng hợp định dạng file Word.

## **3\. Kế hoạch thực hiện Đồ án (Lộ trình 12 Tuần và 7 Chặng tích hợp)**

Kế hoạch kết hợp 4 cột mốc chấm điểm của môn học (Tuần 4 – Tuần 9 – Tuần 11 – Tuần 12\) với 7 chặng triển khai tích hợp kỹ thuật của nhóm.

| Giai đoạn | Thời gian | Chặng tích hợp kỹ thuật | Sản phẩm bàn giao trọng tâm |
| :---- | :---- | :---- | :---- |
| **Giai đoạn 1** | Tuần 1 đến Tuần 4 | Chuẩn hóa 35 bảng CSDL và Hợp đồng API | Báo cáo Đăng ký đề tài, Đặc tả 25 UC và 36 BR, File Excel Từ điển dữ liệu |
| **Giai đoạn 2** | Tuần 5 đến Tuần 9 | Chặng 1, 2, 3 và 4 | Báo cáo Giữa kỳ, 60-70% code chạy thông suốt Web, Desktop và SQL Server |
| **Giai đoạn 3** | Tuần 10 đến Tuần 11 | Chặng 5, 6 và 7 | Báo cáo Cuối kỳ đủ 9 mục, 100% Source Code và bộ kiểm thử xUnit |
| **Giai đoạn 4** | Tuần 12 | Kiểm thử 9 luồng tích hợp liên phân hệ | Demo trực tiếp Web, POS, Admin và Vấn đáp cá nhân |

### **Lộ trình triển khai chi tiết theo từng tuần**

**Giai đoạn 1: Phân tích, Thiết kế và Báo cáo Đăng ký đề tài (Tuần 1 – Tuần 4\)**

* **Tuần 1 (Khảo sát và chốt quy tắc nghiệp vụ):** Thống nhất phạm vi 25 Use Case, 36 Quy tắc nghiệp vụ (BR-01 đến BR-36) và các quy ước kỹ thuật dùng chung; hoàn thành bản thảo Chương 1 và Chương 2\.  
* **Tuần 2 (Đặc tả Use Case và Sơ đồ nghiệp vụ):** Tài đặc tả UC-WEB-01 đến 04, Thái đặc tả UC-WEB-05 đến 08, Phúc đặc tả UC-POS-01 đến 05, Thiện đặc tả UC-ADM-01 đến 12; vẽ Sơ đồ ngữ cảnh, Use Case và Swimlane Workflow.  
* **Tuần 3 (Thiết kế CSDL 35 bảng và Giao diện):** Thiện chốt lược đồ CSDL 35 bảng đạt chuẩn 3NF trên SQL Server; cả nhóm điền xong 4 Sheet Từ điển dữ liệu và Ràng buộc toàn vẹn trên Google Sheets và phác thảo giao diện Web/WinForms.  
* **Tuần 4 (Mốc Báo cáo Đăng ký đề tài):** Nộp và trình bày Báo cáo Giai đoạn 1 (Chương 1, 2, 3), Lược đồ CSDL 3NF, File Excel Từ điển dữ liệu và Bảng phân công nhiệm vụ của 4 thành viên.

**Giai đoạn 2: Xây dựng Cốt lõi (MVP) và Báo cáo Giữa kỳ (Tuần 5 – Tuần 9\)**

* **Tuần 5 — Chặng 1 (Tài khoản, Phân quyền và Ca làm việc):** Thiện dựng khung xác thực/phân quyền và làm UC-ADM-01, 11, 12; Tài hoàn thiện toàn bộ UC-WEB-01, 02, 03 (Đăng ký, Đăng nhập, Hồ sơ và Sổ địa chỉ); Phúc làm UC-POS-01 (Mở/kết thúc ca).  
* **Tuần 6 — Chặng 2 (Thực đơn, BOM và Tồn kho):** Thiện xây dựng UC-ADM-02 đến 06 (Thực đơn, Nguyên liệu, BOM, Tồn kho, Chứng từ kho); Tài xây dựng UC-WEB-04 (Duyệt, lọc thực đơn) và hoàn tất bộ test xUnit cho UC-WEB-01 đến 04\.  
* **Tuần 7 — Chặng 3 (Giỏ hàng, Tạo đơn và Giữ chỗ kho):** Thái xây dựng UC-WEB-05 (Tùy chỉnh món, giỏ hàng), dịch vụ tính tổng tiền/phí giao hàng và UC-WEB-06 (Đặt món đa hình thức kèm giữ chỗ BOM); Phúc làm UC-POS-02 (Tạo đơn tại quầy).  
* **Tuần 8 — Chặng 4 (Thanh toán VietQR và Đối soát):** Thái xây dựng UC-WEB-07 (Sinh mã VietQR động, đối soát chuyển khoản và xử lý đơn hết hạn chờ thanh toán); Phúc tích hợp thu tiền mặt và VietQR tại quầy trong UC-POS-05.  
* **Tuần 9 (Mốc Báo cáo Giữa kỳ):** Demo trực tiếp luồng liên thông từ Chặng 1 đến Chặng 4: Thiện chỉnh giá và nhập kho trên Admin, Tài đăng nhập và tìm lọc món trên Web, Thái tùy chỉnh giỏ hàng, đặt đơn và thanh toán VietQR, Phúc nhận đơn trên POS.

**Giai đoạn 3: Hoàn thiện Nghiệp vụ Nâng cao và Báo cáo Cuối kỳ (Tuần 10 – Tuần 11\)**

* **Tuần 10 — Chặng 5, 6 và 7 (Xử lý đơn, Hủy/Hoàn tiền, Hóa đơn và Báo cáo):** Phúc hoàn thiện UC-POS-03 (Pha chế trừ kho thực tế 1 lần và giảm giữ chỗ), UC-POS-04 (Hủy món chưa pha) và phát hành/xuất hóa đơn PDF (UC-POS-05).  
* **Phối hợp Chặng 5, 6 và 7 trong Tuần 10:** Thái hoàn thiện UC-WEB-08 (Theo dõi đơn thời gian thực, khách hủy đơn chờ xác nhận) và test xUnit cho UC-WEB-05 đến 08; Thiện hoàn thiện UC-ADM-07, 08, 09, 10 (Đơn ngoại lệ, Hóa đơn, Hoàn tiền, Báo cáo).  
* **Tuần 11 (Kiểm thử tích hợp và Nộp Báo cáo Cuối kỳ):** Cùng chạy 9 luồng kiểm thử tích hợp bắt buộc giữa 3 phân hệ và đảm bảo pass 100% kịch bản xUnit; hoàn thiện cuốn Báo cáo cuối kỳ đủ 9 mục kèm File Excel Từ điển dữ liệu và Source Code.

**Giai đoạn 4: Bảo vệ và Vấn đáp Đề tài (Tuần 12\)**

* **Kịch bản Demo liên hoàn (5 – 7 phút):** Thiện nhập kho và công bố giá/BOM trên Admin; Tài đăng nhập, cập nhật sổ địa chỉ và tra cứu thực đơn trên Web; Thái tùy chỉnh món, đặt đơn và quét VietQR; Phúc mở ca, pha chế trừ kho, in hóa đơn trên POS; Thiện duyệt hoàn tiền và xuất báo cáo Excel.  
* **Vấn đáp cá nhân:** Mỗi thành viên nắm vững kiến trúc phân lớp (Presentation – Web API – Domain/Infrastructure – SQL Server) và giải thích chi tiết mã nguồn, giao dịch nguyên tử cùng bộ kiểm thử xUnit thuộc các Use Case mình phụ trách.

