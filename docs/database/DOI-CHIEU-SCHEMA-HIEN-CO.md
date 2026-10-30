# Đối chiếu script hiện có và phương án database gợi ý

Trong dự án đã có **`schema.sql` với 42 bảng** được nhóm lưu trong Git. File này được giữ nguyên, không thực thi. Tài liệu **51 bảng** là một phương án thiết kế đầy đủ để tham khảo, **không phải migration** tương thích trực tiếp với script 42 bảng.

## Hướng đề xuất cho đồ án hiện tại

Nên lấy script 42 bảng làm nền, giữ các tên đã thống nhất trong nhóm, rồi bổ sung từng phần còn cần thiết. Không cần đổi ngay toàn bộ Customers/AdminAccounts thành UserAccounts hoặc ép phải có 51 bảng. Phương án 51 bảng giúp thấy rõ các ranh giới nghiệp vụ khi muốn mở rộng và chuẩn hóa hơn.

| Nội dung | Script 42 bảng hiện có | Gợi ý cân nhắc |
|---|---|---|
| Tài khoản | Customers và AdminAccounts riêng; không có Staff account | Đang phù hợp yêu cầu nhân viên tự nhập tên. Có thể giữ nguyên; gộp UserAccounts chỉ là phương án thay thế. |
| Ca và bàn giao | PosTerminals, Shifts, ShiftHandovers | Giữ quan hệ ca–máy và lịch sử bàn giao; bổ sung sổ tiền mặt để chốt ca theo nguồn thu/chi. |
| Phiên đăng nhập | Chưa thấy bảng lưu phiên | Thêm UserSessions hoặc hai bảng phiên tương ứng Customer/Admin nếu cần thu hồi riêng từng phiên/refresh token. |
| Menu và tùy chọn | OptionGroups, OptionValues, VariantOptions | Cách tổng quát này có thể giữ. OrderItemOptions hiện chưa có số phần; topping nhiều phần/ly cần quantity, giới hạn và ảnh chụp đơn giá/phần. |
| Công thức | Recipes tự mang VersionNumber; RecipeLines và RecipeOptionAdjustments | Đã có phiên bản, không bắt buộc tách RecipeVersions. Cần chốt BOM topping theo phần và bảo toàn định mức lúc gửi đơn. |
| Pha từng phần | OrderItems có StartedQuantity, CompletedQuantity, CancelledQuantity và CHECK | Đây là nền hợp lệ. PreparationBatches là phần mở rộng để truy vết mỗi lần bắt đầu pha; vẫn cần transaction chống race với hủy. |
| Giữ chỗ và sổ kho | InventoryBalances, InventoryReservations, StockMovements, OperationKey | Đã có nền chống trừ trùng và sổ kho; kiểm tra cập nhật số dư/reservation/movement cùng transaction. |
| Kho và nhà cung cấp | Một số dư theo Ingredient; có Suppliers, StockDocuments/Lines | Một kho là đủ giai đoạn đầu. Chỉ thêm Warehouses khi thực sự có nhiều kho; có thể giữ Suppliers chuẩn hóa. |
| Phí giao hàng | DeliveryFee và địa chỉ snapshot trên Orders | Cần thêm cấu hình chính sách/báo phí có thời hạn nếu muốn quản lý phiên bản giá ship và xác nhận lại khi đổi địa chỉ. |
| Yêu cầu chuyển khoản | Payments kết hợp ExpectedAmount, thời hạn và số VerifiedAmount | Có thể dùng trong bản gọn, nhưng tách PaymentRequests khỏi khoản thu thực tế sẽ làm rõ QR được tạo khác với tiền đã nhận. |
| Giao dịch ngân hàng | BankTransactions có mã provider duy nhất và PaymentId nullable | Đã giữ khoản chưa ghép được. Cần xác minh nguồn, chống lặp webhook, định danh tài khoản nhận và quản lý giao dịch chi nếu tích hợp hoàn. |
| Hoàn tiền | Refunds + RefundAttempts, mỗi Refund tối đa một attempt SUCCEEDED | Phù hợp nếu một hồ sơ hoàn được chi trọn một lần; nếu cho nhiều lần chi thành công phải thêm Amount vào attempt và thay unique tương ứng. Không chỉ xóa index mà bỏ kiểm soát hạn mức. |
| Hóa đơn | Invoices có DocumentSnapshot JSON và hóa đơn điều chỉnh | Có thể giữ snapshot JSON; InvoiceLines là tùy chọn để truy vấn dòng hàng dễ hơn, không được làm hai nguồn dữ liệu mâu thuẫn. |
| Chống gửi lặp | CommandRequests + payload hash | Giữ lại, làm rõ thời gian lưu, phạm vi key, quyền tác nhân và trạng thái chưa rõ sau timeout. |
| Real-time / inbox | Chưa thấy OutboxMessages hoặc PaymentEvents | Nên thêm khi triển khai tích hợp: outbox ghi cùng transaction; inbox webhook có unique event key và xác minh nguồn. |
| Audit | AuditLogs có actor Customer/Admin/Shift/System | Giữ; bổ sung tên/mã máy snapshot nếu cần giữ lịch sử tự khai, che dữ liệu nhạy cảm và tách quyền xuất. |

## Điều kiện nghiệp vụ cần giữ trong cả hai phương án

1. Web chỉ nhận CK trả trước; POS có CASH hoặc CK, không thu lại đơn Web.
2. Tiêu hao tại lúc bắt đầu pha, không trừ lần hai lúc hoàn tất; hủy phần chưa pha giải phóng giữ chỗ, không nhập kho giả.
3. Lượng hủy và đã pha không vượt số đặt; transaction/version xử lý cạnh tranh giữa hai thao tác.
4. Tổng hoàn thành công và khoản đang chi/đối soát không vượt nghĩa vụ còn phải hoàn.
5. Không đọc giá/BOM mới nhất để tính lại đơn cũ; giữ snapshot và phiên bản đã chốt.
6. Nguồn chứng từ đã ghi sổ không được sửa/xóa; điều chỉnh bằng chứng từ có liên kết nguồn.

**Chưa làm:** sửa cấu trúc SQL, chạy script, tạo DB, đổi dữ liệu hoặc viết migration. Cần chọn phương án trước khi sinh DDL/EF entities. Các sơ đồ trong `ERD.md` đang minh họa phương án 51 bảng, không mô tả nguyên trạng file `schema.sql`.
