# Thiết kế database đề xuất — 4CHANG LINH NGU LAM

**Bản gợi ý để thảo luận, chưa tạo database hoặc EF migration.** Đề xuất một CSDL SQL Server `FourChangDb`, dùng chung qua API cho Website và hai Desktop. Phạm vi bám 8 UC Customer, 5 UC Staff, 11 UC Admin. Giao diện hiện tại và dữ liệu demo không thay đổi theo tài liệu này.


**Đối chiếu hiện trạng:** dự án có sẵn `schema.sql` 42 bảng. Phương án 51 bảng dưới đây là thiết kế mở rộng tham khảo, không phải migration thay thế script đó. Nên đọc [đối chiếu và hướng áp dụng](DOI-CHIEU-SCHEMA-HIEN-CO.md) trước khi chọn mô hình.

## 1. Quyết định nền tảng

- Có **51 bảng** cho phương án đầy đủ. Nhiều bảng là bảng nối, lịch sử và sổ giao dịch; không có nghĩa từng bảng cần một màn hình. Có thể triển khai theo từng nhóm UC, không cần viết hết backend ngay.
- `UserAccounts` chỉ có CUSTOMER/ADMIN. **Không có bảng tài khoản nhân viên**. Staff tự nhập tên vào `WorkSessions`, luôn gắn `PosDeviceId`; quyền máy POS tách khỏi quyền quản trị.
- Tách ba vòng đời: đơn/pha chế, thu tiền, chi hoàn. `Orders.Status` không chứa `WAITING_REFUND`; giao diện ghép trạng thái hủy đơn với hồ sơ hoàn tiền để hiện “Chờ hoàn tiền”.
- Giữ snapshot giá, tên món, địa chỉ, topping và BOM trên đơn. Menu/công thức thay đổi không sửa lịch sử.
- Một lần bắt đầu pha có `PreparationBatches`; trừ BOM ở đây **đúng một lần**. Hoàn tất không trừ lần nữa. Hoàn tiền không tự hoàn kho đã tiêu hao.
- Hệ thống real-time sử dụng outbox + API/SignalR. WinForms không truy cập trực tiếp SQL Server.

## 2. Quy ước dữ liệu

| Thành phần | Đề xuất |
|---|---|
| PK mặc định | `Id bigint IDENTITY`; bảng nối dùng PK ghép ghi rõ ở từ điển. Mã nghiệp vụ là cột riêng có UNIQUE. |
| FK | Cùng kiểu với PK nguồn; dấu `?` là nullable. FK trong từ điển được ghi bằng mũi tên. |
| Chuỗi tiếng Việt | `nvarchar(n)`; mã trạng thái/loại dùng `varchar(n)` + CHECK hoặc enum mapping. |
| Tiền | `decimal(19,2)`; Currency `char(3)` mặc định VND; làm tròn VND theo chính sách, không dùng float/double. |
| Lượng nguyên liệu/hệ số | `decimal(18,6)`; số ly/số phần là `int`, kiểm tra dương theo nghiệp vụ. |
| Thời gian | `datetime2(3)` UTC; hiển thị giờ Asia/Bangkok; báo cáo quy đổi biên ngày trước truy vấn. |
| Chống ghi đè | `RowVersion rowversion` ở Orders, OrderItems, WorkSessions, InventoryBalances, Refunds và cấu hình đang sửa. Không dùng làm ngày giờ hoặc khóa chính. |
| Metadata | Bảng master có CreatedAt, UpdatedAt, IsActive khi phù hợp; không lặp trong mọi dòng từ điển. Ledger/history chỉ append và có OccurredAt. |
| Xóa | Master đã tham chiếu: ngừng hoạt động. Giao dịch đã phát hành/ghi sổ: không xóa cascade; sửa sai bằng chứng từ liên kết. |
| Dữ liệu nhạy cảm | Hash mật khẩu/refresh token; mã hóa tài khoản nhận hoàn, phân quyền dữ liệu nguyên bản; audit che thông tin bí mật. |

SQL Server `rowversion` là số nhị phân phiên bản, không chứa ngày giờ. EF Core có thể map thành concurrency token và báo xung đột khi phiên bản thay đổi. [SQL Server rowversion](https://learn.microsoft.com/en-us/sql/t-sql/data-types/rowversion-transact-sql), [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

## 3. Từ điển bảng

Các cột dưới đây là mô hình logic để chốt nghiệp vụ trước khi viết DDL. Những ràng buộc liên bảng/tổng cộng không được giả định là một CHECK SQL đơn giản; phần 6 chỉ rõ nơi cần transaction/service.

### Tài khoản, phân quyền và ca làm việc

| Bảng | Khóa chính | Cột chính và FK | Ràng buộc / mục đích |
|---|---|---|---|
| `UserAccounts` | `Id` | AccountType, LoginNormalized, EmailNormalized?, PhoneNormalized?, DisplayName, PasswordHash, MustChangePassword, IsActive, TokenVersion | AccountType chỉ CUSTOMER hoặc ADMIN; không có tài khoản Staff. Định danh chuẩn hóa duy nhất; mật khẩu là hash, không mã hóa thuận nghịch. |
| `UserSessions` | `Id` | UserId → UserAccounts, RefreshTokenHash, Scope, ExpiresAt, RevokedAt?, LastSeenAt | Thu hồi từng phiên khi đăng xuất; hash refresh token duy nhất. Scope tạm đổi mật khẩu không được đặt hàng. |
| `CustomerAddresses` | `Id` | CustomerUserId → UserAccounts, ReceiverName, Phone, AddressText, Latitude?, Longitude?, IsDefault, IsActive | Chỉ Customer sở hữu; tối đa một địa chỉ mặc định đang hoạt động. Đơn đã gửi dùng bản chụp riêng. |
| `Roles` | `Id` | Code, Name, IsActive | Code duy nhất; vai trò quản trị được cấu hình; Customer không được gán quyền nội bộ. |
| `UserRoles` | `(UserId, RoleId)` | UserId → UserAccounts, RoleId → Roles | PK ghép; áp dụng cho người có tài khoản; không gắn tên nhân viên tự khai vào bảng này. |
| `Permissions` | `Id` | Code, Name, Module | Code duy nhất, ví dụ Refund.Approve, Audit.Read, Audit.Export, Inventory.Post. |
| `RolePermissions` | `(RoleId, PermissionId)` | RoleId → Roles, PermissionId → Permissions | PK ghép; quyền báo cáo, audit và xuất độc lập. |
| `PosDevices` | `Id` | TerminalCode, Name, CredentialHash, IsActive, RevokedAt? | TerminalCode duy nhất; dùng tập quyền POS cố định của dịch vụ, không tạo tài khoản nhân viên. |
| `WorkSessions` | `Id` | PosDeviceId → PosDevices, DeclaredStaffName, StartedAt, EndedAt?, Status, OpeningCash, CountedCash?, DifferenceReason?, HandoverFromId? → WorkSessions, ApprovedByAdminId? → UserAccounts | Tên tự khai + mã ca + thiết bị; tối đa một ca chưa đóng trên máy. Khóa/tiếp tục là trạng thái ca, không cấp quyền Admin. |
| `CashMovements` | `Id` | WorkSessionId → WorkSessions, PaymentId? → Payments, RefundAttemptId? → RefundAttempts, Kind, Amount, Reason, OperationKey | Sổ tiền mặt bất biến; tham chiếu nguồn có điều kiện; unique OperationKey. Chi ngoài bán hàng cần Admin duyệt/audit. |

### Thực đơn và nội dung

| Bảng | Khóa chính | Cột chính và FK | Ràng buộc / mục đích |
|---|---|---|---|
| `Categories` | `Id` | Code, Name, SortOrder, IsPublished, IsActive | Mã duy nhất; ngừng sử dụng khi có món liên quan. |
| `Products` | `Id` | CategoryId → Categories, Code, Name, Description, ImagePath, IsPublished, IsSelling, PublishedAt? | Trạng thái bật bán khác trạng thái đủ nguyên liệu; món mới theo PublishedAt, bán chạy tính từ giao dịch. |
| `ProductVariants` | `Id` | ProductId → Products, SizeCode, TemperatureCode, Price, IsEnabled | Unique(ProductId, SizeCode, TemperatureCode); size S/M/L, nhiệt HOT/ICED; giá không âm. |
| `Toppings` | `Id` | Code, Name, PricePerPortion, IsEnabled | Đơn vị phần; giá không âm; BOM topping được quản lý bằng Recipes. |
| `ProductToppings` | `(ProductId, ToppingId)` | ProductId → Products, ToppingId → Toppings, MaxPortionsPerCup, IsEnabled | Giới hạn số phần/ly và quan hệ món–topping hợp lệ. |
| `Banners` | `Id` | Title, ImagePath, TargetUrl?, Placement, StartsAt, EndsAt?, SortOrder, IsPublished | EndsAt > StartsAt nếu có; thay banner không thay dữ liệu giao dịch. |

### Nguyên liệu và công thức BOM

| Bảng | Khóa chính | Cột chính và FK | Ràng buộc / mục đích |
|---|---|---|---|
| `Units` | `Id` | Code, Name, Dimension | Đơn vị cơ sở/nhập như g, kg, ml, l, chai; không tự quy đổi khối lượng sang thể tích. |
| `Ingredients` | `Id` | Code, Name, BaseUnitId → Units, SafetyQuantity, LowStockThreshold, IsActive | Các ngưỡng không âm; không chứa một cột tồn kho cho mọi kho. |
| `IngredientUnitConversions` | `(IngredientId, FromUnitId)` | IngredientId → Ingredients, FromUnitId → Units, FactorToBaseUnit | Hệ số > 0; 1 chai của từng nguyên liệu có thể khác nhau. Snapshot quy đổi trên dòng chứng từ. |
| `Recipes` | `Id` | ProductVariantId? → ProductVariants, ToppingId? → Toppings, IsActive | CHECK XOR: đúng một chủ sở hữu là biến thể món hoặc topping; unique FK không null. |
| `RecipeVersions` | `Id` | RecipeId → Recipes, VersionNo, Status, EffectiveFrom?, EffectiveTo?, CreatedByAdminId → UserAccounts | Unique(RecipeId, VersionNo); bản DRAFT sửa được; đã dùng/công bố không ghi đè. API chặn khoảng hiệu lực chồng nhau. |
| `RecipeIngredients` | `(RecipeVersionId, IngredientId)` | RecipeVersionId → RecipeVersions, IngredientId → Ingredients, QuantityPerServing, LossRate | Định mức theo đơn vị cơ sở; lượng > 0, 0 ≤ hao hụt < 1. Cần định nghĩa rõ lượng này là NET hay GROSS; đề xuất NET. |
| `RecipeOptionAdjustments` | `Id` | RecipeVersionId → RecipeVersions, OptionType, PercentValue, IngredientId → Ingredients, ReplacementNetQuantity | Unique(version, loại tùy chọn, mức, nguyên liệu); đường/đá là cấu trúc. Lượng thay thế có thể 0; chặn hai loại cùng ghi đè một nguyên liệu nếu chưa có quy tắc kết hợp. |

### Bàn, giao hàng và đơn hàng

| Bảng | Khóa chính | Cột chính và FK | Ràng buộc / mục đích |
|---|---|---|---|
| `DiningTables` | `Id` | TableCode, Name, IsActive, QrTokenHash, QrVersion, QrExpiresAt? | Mã bàn duy nhất; lưu hash token, xoay version vô hiệu QR cũ; đơn giữ TableCode snapshot. |
| `DeliveryRules` | `Id` | VersionNo, OriginLatitude, OriginLongitude, BaseDistanceKm, BaseFee, StepDistanceKm, FeePerStep, MaxDistanceKm, FreeShippingThreshold?, EffectiveFrom, EffectiveTo? | Chính sách có phiên bản; ngưỡng và làm tròn xác định rõ. Không ghi cố định 15.000đ cho mọi đơn. |
| `DeliveryQuotes` | `Id` | CustomerUserId → UserAccounts, RuleId → DeliveryRules, AddressSnapshot, Latitude, Longitude, DistanceKm, ItemsAmountSnapshot, ShippingFee, ExpiresAt, Status | Báo phí gắn địa chỉ, giá trị giỏ và phiên bản chính sách. Lưu số phí cuối cùng; hết hạn/đổi địa chỉ phải báo lại. |
| `Orders` | `Id` | OrderNo, Channel, FulfillmentType, CustomerUserId? → UserAccounts, WorkSessionId? → WorkSessions, TableId? → DiningTables, DeliveryQuoteId? → DeliveryQuotes, Status, ReceiverNameSnapshot?, PhoneSnapshot?, AddressSnapshot?, TableCodeSnapshot?, ItemSubtotalSnapshot, ShippingFeeSnapshot, OriginalTotalSnapshot, CurrentObligation, AcceptedAt?, CompletedAt?, ExpiresAt? | OrderNo duy nhất; WEB cần Customer; POS cần ca. DINE_IN cần bàn; DELIVERY cần địa chỉ/quote. Một trạng thái xử lý đơn riêng, không trộn trạng thái thanh toán/hoàn. |
| `OrderItems` | `Id` | OrderId → Orders, ProductVariantId → ProductVariants, RecipeVersionId → RecipeVersions, ProductNameSnapshot, SizeSnapshot, TemperatureSnapshot, SugarPercent, IcePercent, UnitBasePriceSnapshot, Quantity, Note?, Status | Quantity > 0; số lượng hủy/đã pha được đối chiếu từ bảng con. Snapshot không sửa theo menu mới; ghi chú không tự thay BOM. |
| `OrderItemToppings` | `Id` | OrderItemId → OrderItems, ToppingId → Toppings, RecipeVersionId → RecipeVersions, NameSnapshot, PortionsPerCup, UnitPriceSnapshot | Unique(OrderItemId, ToppingId); số phần > 0 và nằm trong giới hạn tại thời điểm đặt. |
| `OrderItemIngredients` | `(OrderItemId, IngredientId)` | OrderItemId → OrderItems, IngredientId → Ingredients, BaseUnitId → Units, GrossQuantityPerCup | Snapshot BOM đã áp dụng biến thể, đường/đá, số phần topping và hao hụt; gộp nguyên liệu trùng. Không tính lại từ BOM đang công bố khi bắt đầu pha. |
| `PreparationBatches` | `Id` | OrderItemId → OrderItems, WorkSessionId → WorkSessions, Quantity, Status, StartedAt, ReadyAt?, OperationKey | Một lần bắt đầu pha một phần số ly. Unique OperationKey; tổng batch + phần hủy không vượt số đặt. |
| `OrderStatusHistory` | `Id` | OrderId → Orders, OrderItemId? → OrderItems, FromStatus, ToStatus, ActorUserId? → UserAccounts, WorkSessionId? → WorkSessions, Reason?, OccurredAt, CorrelationId | Lịch sử append-only; giữ cả ca/máy và tên snapshot khi Staff thực hiện. |
| `Cancellations` | `Id` | OrderId → Orders, RequestedByUserId? → UserAccounts, WorkSessionId? → WorkSessions, Status, Reason, RequestedAt, AcceptedAt?, ShippingAdjustment, OperationKey | Lý do bắt buộc; Customer chỉ yêu cầu lúc Chờ xác nhận; Staff chỉ hủy phần chưa pha. Duyệt hủy không đồng nghĩa chi hoàn. |
| `CancellationItems` | `(CancellationId, OrderItemId)` | CancellationId → Cancellations, OrderItemId → OrderItems, Quantity, AmountSnapshot | Quantity > 0; cùng Order với phiếu hủy; tổng hủy được chấp nhận không vượt phần chưa bắt đầu pha. |

### Kho và chứng từ

| Bảng | Khóa chính | Cột chính và FK | Ràng buộc / mục đích |
|---|---|---|---|
| `Warehouses` | `Id` | Code, Name, IsActive | Ban đầu một kho quán; bảng này cho phép thêm kho mà không đổi cấu trúc tồn. |
| `InventoryBalances` | `(WarehouseId, IngredientId)` | WarehouseId → Warehouses, IngredientId → Ingredients, OnHandQuantity, ReservedQuantity, RowVersion | Số dư nhanh phải khớp sổ kho; lượng không âm và Reserved ≤ OnHand. Khả dụng = OnHand − Reserved − SafetyQuantity. |
| `InventoryDocuments` | `Id` | DocumentNo, WarehouseId → Warehouses, Type, Status, SupplierName?, Reason?, InventoryCutoffAt?, CreatedByAdminId → UserAccounts, PostedAt?, ReversalOfId? → InventoryDocuments, OperationKey | IMPORT/EXPORT/COUNT_ADJUST; DRAFT/POSTED/REVERSED. Chỉ nháp được sửa/xóa. Ghi sổ toàn phiếu trong một transaction. |
| `InventoryDocumentItems` | `Id` | DocumentId → InventoryDocuments, IngredientId → Ingredients, InputUnitId → Units, InputQuantity, ConversionFactorSnapshot, BaseQuantity, BookQuantitySnapshot?, CountedQuantity?, UnitCost? | Nhập/xuất dùng lượng dương; điều chỉnh tính delta từ thực đếm và số sổ đã đối chiếu. Không lấn phần giữ chỗ. |
| `InventoryMovements` | `Id` | WarehouseId → Warehouses, IngredientId → Ingredients, DocumentItemId? → InventoryDocumentItems, PreparationBatchId? → PreparationBatches, QuantityDelta, Kind, OccurredAt, ReversalOfId? → InventoryMovements | Sổ bất biến, delta khác 0. Đúng một nguồn chứng từ hoặc batch pha; bản đảo liên kết nguồn gốc. Unique batch + nguyên liệu + kho + loại tiêu hao để không trừ trùng. |
| `IngredientReservations` | `Id` | OrderItemId → OrderItems, IngredientId → Ingredients, WarehouseId → Warehouses, ReservedQuantity, ConsumedQuantity, ReleasedQuantity, ExpiresAt?, Status | Unique(item, nguyên liệu, kho); Consumed + Released ≤ Reserved. Giữ chỗ không phải xuất kho; hết hạn/hủy giải phóng phần chưa pha. |

### Thu tiền, hoàn tiền và hóa đơn

| Bảng | Khóa chính | Cột chính và FK | Ràng buộc / mục đích |
|---|---|---|---|
| `PaymentRequests` | `Id` | OrderId → Orders, RequestCode, Method, AmountDueSnapshot, Currency, QrPayload?, ExpiresAt, Status, OperationKey | QR/VietQR là yêu cầu thu; không là bằng chứng đã thu. Có thể có yêu cầu cũ hết hạn; không dùng trạng thái này làm sổ tiền. |
| `BankTransactions` | `Id` | Provider, MerchantAccountKey, ProviderTransactionId, Direction, Amount, Currency, BankOccurredAt, ReferenceText, VerificationStatus, MatchedAt? | Unique(provider, tài khoản, mã giao dịch); ghi cả khoản chưa ghép đơn. Chỉ nguồn tin cậy được đánh VERIFIED; lưu/che dữ liệu theo phạm vi truy cập. |
| `Payments` | `Id` | OrderId → Orders, PaymentRequestId? → PaymentRequests, BankTransactionId? → BankTransactions, WorkSessionId? → WorkSessions, Method, Amount, ReceivedAt, VerifiedAt?, Status, OperationKey | Thu CASH hoặc BANK_TRANSFER. WEB chỉ BANK_TRANSFER đã xác minh. Unique BankTransactionId không null; một khoản thu ngân hàng chỉ ghép một đơn trong thiết kế ban đầu. |
| `Refunds` | `Id` | OrderId → Orders, CancellationId? → Cancellations, PaymentId? → Payments, RequestedAmount, Reason, RecipientNameSnapshot, RecipientAccountEncrypted, RecipientAccountMasked, Status, RequestedByUserId? → UserAccounts, ApprovedByAdminId? → UserAccounts, ApprovedAt? | Hoàn do hủy hoặc thu thừa/tiền đến muộn. Kiểm tra nghĩa vụ thực, không dựa riêng RequestedAmount. Tài khoản nhận là dữ liệu nhạy cảm; audit chỉ lưu bản che. |
| `RefundAttempts` | `Id` | RefundId → Refunds, Amount, OperationKey, ProviderRequestKey?, BankTransactionId? → BankTransactions, Status, StartedAt, ResolvedAt?, VerifiedByAdminId? → UserAccounts, EvidenceReference?, FailureReason? | Mỗi lần chi là bản ghi riêng; unique OperationKey và mã ngân hàng không null. PROCESSING/RECONCILING giữ hạn mức; chỉ SUCCEEDED cộng số đã hoàn. |
| `Invoices` | `Id` | InvoiceNo, OrderId → Orders, Type, OriginalInvoiceId? → Invoices, SourceCancellationId? → Cancellations, Status, BuyerSnapshot, TotalAmount, IssuedAt, IssuedByUserId? → UserAccounts, WorkSessionId? → WorkSessions | Hóa đơn bán hàng nội bộ: SALE/ADJUSTMENT. Unique InvoiceNo; bản phát hành bất biến; điều chỉnh có nguồn, không ghi đè bản gốc. In lại không tạo hóa đơn mới. |
| `InvoiceLines` | `Id` | InvoiceId → Invoices, OrderItemId? → OrderItems, DescriptionSnapshot, Quantity, UnitPriceSnapshot, LineAmount | Lưu dòng hàng/topping/phí đã phát hành. Dòng điều chỉnh có thể có giá trị âm theo loại chứng từ; tổng dòng bằng đầu hóa đơn. |

### Tích hợp, chống trùng và nhật ký

| Bảng | Khóa chính | Cột chính và FK | Ràng buộc / mục đích |
|---|---|---|---|
| `PaymentEvents` | `Id` | Provider, ProviderEventId, ReceivedAt, VerifiedAt?, PayloadHash, PayloadReference, ProcessingStatus, BankTransactionId? → BankTransactions, ErrorCode? | Unique(provider, eventId); inbox webhook. Nhận lặp trả kết quả cũ; xác minh nguồn trước khi ghi nhận tiền. |
| `AuditLogs` | `Id` | ActorUserId? → UserAccounts, WorkSessionId? → WorkSessions, ActorNameSnapshot, TerminalCodeSnapshot?, Action, EntityType, EntityId, BeforeJson?, AfterJson?, Reason?, OccurredAt, CorrelationId | Append-only; che token/password/số tài khoản; quyền đọc/xuất độc lập. Tên Staff luôn gắn ca/máy, không coi là tài khoản xác thực. |
| `OutboxMessages` | `Id` | EventType, AggregateType, AggregateId, AggregateVersion, Payload, CreatedAt, PublishedAt?, RetryCount, NextAttemptAt?, ErrorCode? | Ghi cùng transaction nghiệp vụ, worker phát SignalR/event sau commit; consumer chống trùng. Không giả định exactly-once delivery. |
| `IdempotencyRequests` | `Id` | PrincipalScope, OperationType, RequestKey, PayloadHash, Status, ResponseReference?, CreatedAt, ExpiresAt | Unique(scope, loại lệnh, key); cùng key khác payload bị từ chối. Thời gian giữ key bao phủ thời gian retry/đối soát. |

## 4. Quan hệ quan trọng

| Quan hệ | Bội số | Giải thích |
|---|---|---|
| Customer → Orders | 1–N | POS có thể là khách lẻ: CustomerUserId null, nhưng phải có WorkSessionId. |
| PosDevices → WorkSessions → Orders | 1–N–N | Truy ngược nhân viên tự khai và máy thực hiện. Không FK tên nhân viên sang UserAccounts. |
| Products → ProductVariants → OrderItems | 1–N–N | Đơn lưu cấu hình và đơn giá snapshot, không chỉ giữ ProductId. |
| OrderItems → OrderItemToppings / Ingredients | 1–N | Topping theo phần/ly; nguyên liệu snapshot đã tổng hợp. |
| OrderItems → PreparationBatches → InventoryMovements | 1–N–N | Hỗ trợ 2 ly đặt, 1 ly đã pha, 1 ly chưa pha; chỉ ly chưa pha có thể hủy. |
| Orders → Payments / Refunds / Invoices | 1–N mỗi loại | Thu thiếu/nhiều lần, nhiều hồ sơ hoàn hoặc chứng từ điều chỉnh không bị ép vào một cột. |
| BankTransactions → Payments | 1–0..1 | Ban đầu mỗi khoản thu ngân hàng ghép tối đa một đơn. Nếu sau này cần tách một khoản cho nhiều đơn, bổ sung PaymentAllocations; chưa cần ở phạm vi này. |
| Refunds → RefundAttempts | 1–N | Lưu lần chi, thất bại và đối soát riêng; không sửa mất lịch sử retry. |
| Recipes → RecipeVersions → RecipeIngredients | 1–N–N | Phiên bản cũ được đơn tham chiếu; bản mới không đổi dữ liệu cũ. |

Sơ đồ trọng tâm nằm trong `ERD.md`; từ điển bảng phía trên là phạm vi đầy đủ, sơ đồ chỉ lược bớt metadata để dễ đọc.

## 5. Trạng thái đề xuất

| Đối tượng | Giá trị |
|---|---|
| Orders | PENDING_PAYMENT → PENDING_CONFIRMATION → CONFIRMED → PREPARING → READY → DELIVERING nếu giao hàng → COMPLETED; nhánh CANCELLED, EXPIRED, FULFILLMENT_FAILED. POS chưa thu có thể được CONFIRMED nhưng chặn bàn giao/hoàn tất khi chưa thu đủ theo quy tắc đã thống nhất. |
| PreparationBatches | PREPARING → READY → SERVED/HANDED_OVER; sự cố sau pha được quản lý giải quyết, không tua về chưa pha. |
| PaymentRequests | CREATED, WAITING, EXPIRED, CLOSED; số tiền thực nhận được đọc từ Payments/BankTransactions đã xác minh. |
| Payments | PENDING, VERIFIED, REJECTED; chỉ VERIFIED được tính thực thu. Thanh toán đã xác minh không đổi thành chưa thu khi phát hành hóa đơn lỗi. |
| Refunds | REQUESTED, APPROVED, PROCESSING, RECONCILING, PARTIALLY_SUCCEEDED, SUCCEEDED, REJECTED; từ chối hồ sơ sai không tự xóa nghĩa vụ còn phải hoàn của đơn. |
| RefundAttempts | CREATED, PROCESSING, RECONCILING, SUCCEEDED, FAILED; timeout là RECONCILING, không tự tạo chi mới. |
| WorkSessions | OPEN, LOCKED, HANDED_OVER, CLOSED; một máy tối đa một ca chưa kết thúc. |
| InventoryDocuments | DRAFT → POSTED; sửa sai bằng chứng từ đảo/điều chỉnh. REVERSED chỉ khi việc đảo đã được ghi sổ thành công. |
| RecipeVersions | DRAFT → PUBLISHED → RETIRED; không sửa định mức của phiên bản đã dùng. |

Đây là state machine gợi ý. Trước migration, chốt sự khác nhau giữa trạng thái tổng hợp ở đầu đơn và trạng thái từng phần món; không cho UI tự gán một trạng thái làm mất các phần còn đang xử lý.

## 6. Các quy tắc phải bảo đảm khi triển khai

### 6.1 Tạo đơn và giữ chỗ

1. API tính lại giá, lựa chọn và phí giao; kiểm tra quote/QR bàn/phiên. Không tin tổng tiền từ UI.
2. Đọc BOM hiệu lực, áp dụng đường/đá có cấu trúc; mỗi topping nhân số phần/ly. Đề xuất lượng NET đã điều chỉnh rồi chia `(1 − LossRate)` để ra lượng GROSS; không cộng hao hụt lần hai. Làm tròn lượng theo chính sách đơn vị trước snapshot.
3. Ghi Orders, Items, Toppings, IngredientSnapshots; giữ chỗ theo nhu cầu, cùng một transaction. Đảm bảo tồn khả dụng không âm trong cạnh tranh Web/POS bằng cập nhật có điều kiện/khóa cùng các dòng số dư.
4. Web tạo yêu cầu CK trả trước; hết hạn chưa nhận tiền thì EXPIRED và giải phóng phần giữ chỗ còn lại. Tiền đến muộn phải đối soát, không tự phục hồi đơn và giữ chỗ cũ.

### 6.2 Trừ kho và pha từng phần

1. Đọc version của dòng đơn và kiểm tra số ly chưa pha/chưa hủy.
2. Trong cùng transaction: tạo PreparationBatch với OperationKey duy nhất; ghi InventoryMovements âm từ BOM snapshot × số ly; giảm OnHand và Reserved tương ứng; đánh dấu phần giữ chỗ đã tiêu hao; ghi history/audit/outbox.
3. Retry cùng key trả batch cũ, không ghi chuyển động kho mới. Hoàn tất chỉ cập nhật tiến trình.
4. Khi hủy trước pha, giải phóng reservation của phần chưa pha; không ghi nhập kho giả. Sự cố sau pha xử lý bằng quyết định/chứng từ phù hợp, không tự đảo tiêu hao.

### 6.3 Thu tiền và VietQR

- Web chỉ BANK_TRANSFER đã xác minh. QR được tạo không có nghĩa đã thu; callback phải xác minh nguồn và chống trùng bằng ProviderEventId/ProviderTransactionId.
- POS cho CASH hoặc BANK_TRANSFER; tiền khách đưa và tiền thừa có thể lưu vào metadata receipt/cột bổ sung trên Payments, nhưng Amount là tiền thực thu cho nghĩa vụ, không phải toàn bộ tiền khách đưa.
- Đơn Web không tạo khoản thu mới khi nhân viên tiếp nhận hoặc in hóa đơn. Khoản thiếu/thừa và khoản chưa ghép được giữ để đối soát.
- Một giao dịch ngân hàng không được dùng đồng thời làm khoản thu hoặc chứng cứ chi sai chiều. FK chỉ bảo đảm tồn tại; API kiểm tra Direction/Amount/VerificationStatus trong transaction.

### 6.4 Hủy và hoàn tiền

Ký hiệu: **G** = nghĩa vụ hiện hành sau điều chỉnh hợp lệ; **P** = tổng khoản thu VERIFIED; **R** = tổng RefundAttempts SUCCEEDED.

```text
Còn thu         = max(0, G − P + R)
Còn phải hoàn   = max(0, P − R − G)
Hạn mức chi mới = max(0, P − R − G − U)
```

U là số tiền các lần chi PROCESSING/RECONCILING chưa rõ kết quả đang giữ hạn mức, chưa nằm trong R. Duyệt/chi cần khóa cùng ngữ cảnh Order hoặc thực hiện chiến lược transaction tương đương; hai người duyệt không được cùng dùng một hạn mức. Failed chắc chắn mới giải phóng U. Không cộng cả “hồ sơ hoàn” lẫn “lần chi hoàn” vào R gây trùng tiền.

Customer chỉ yêu cầu khi PENDING_CONFIRMATION; Staff hủy phần chưa bắt đầu pha với lý do. Phí ship hoàn toàn hay một phần phải là chính sách được duyệt và snapshot vào Cancellation.ShippingAdjustment, không mặc định tự hoàn/giữ toàn bộ.

### 6.5 Tồn, món hết và công thức

- `Available = OnHand − Reserved − SafetyQuantity`; “sắp hết” dùng ngưỡng riêng. Thiếu một topping chỉ khóa topping đó; thiếu một biến thể chỉ khóa biến thể đó. Món hết khi không còn cấu hình được bán hợp lệ.
- OrderItemIngredients là nguồn định mức cho việc tiêu hao đơn cũ; không lấy RecipeVersions mới nhất ở thời điểm pha.
- UnitConversions theo nguyên liệu; một “chai” không có hệ số toàn hệ thống. Không dùng ghi chú “ít ngọt” để tự thay BOM; nhân viên cần chọn cấu hình được khai báo.
- InventoryBalances là projection có thể đối chiếu lại từ ledger + reservation. Ghi sổ phiếu, ledger và số dư phải commit cùng nhau.

### 6.6 Báo cáo và tiền mặt

- Tách báo cáo doanh thu theo chính sách ghi nhận từ chứng từ bán/điều chỉnh và báo cáo dòng tiền thu/chi thực tế. Không trừ hoàn hai lần qua cả hóa đơn điều chỉnh lẫn số chi hoàn trên cùng chỉ tiêu.
- Tiền mặt dự kiến ca = tiền đầu ca + thu CASH − chi/hoàn CASH đã duyệt. Không cộng CK hoặc khoản chưa xác minh vào két tiền.
- Top món/tỷ lệ hủy tính từ số lượng được chấp nhận/hủy/hoàn tất có định nghĩa rõ; mẫu số 0 không phải lỗi truy vấn. Quyền Audit.Read/Audit.Export riêng với Reports.Read/Reports.Export.

## 7. Unique/index và kiểm tra

| Loại | Đề xuất |
|---|---|
| UNIQUE | LoginNormalized; OrderNo; InvoiceNo; DocumentNo; TerminalCode; variant(product,size,temp); version(recipe,no); balance(kho,nguyên liệu); operation keys. |
| UNIQUE có lọc | EmailNormalized/PhoneNormalized khi không null; Payment.BankTransactionId khi không null; RefundAttempt.BankTransactionId khi không null; CustomerAddresses theo CustomerUserId khi IsDefault=1 và IsActive=1. |
| Một ca trên máy | Unique filtered PosDeviceId với `EndedAt IS NULL`; transaction giữ EndedAt nhất quán trạng thái OPEN/LOCKED. |
| Theo hàng đợi | Orders(Status, CreatedAt) INCLUDE(Channel, FulfillmentType, CurrentObligation); OrderItems(OrderId,Status). |
| Tra lịch sử | Orders(CustomerUserId,CreatedAt DESC); Orders(WorkSessionId,CreatedAt); Payments(OrderId,Status); Refunds(OrderId,Status); RefundAttempts(RefundId,Status). |
| Sổ kho và log | InventoryMovements(WarehouseId,IngredientId,OccurredAt); AuditLogs(EntityType,EntityId,OccurredAt); AuditLogs(CorrelationId). |
| Worker | OutboxMessages(PublishedAt,NextAttemptAt); PaymentEvents(ProcessingStatus,ReceivedAt). |

Filtered index chỉ đánh chỉ mục một tập hàng thỏa điều kiện, phù hợp các unique nullable/địa chỉ mặc định ở trên. [Microsoft: filtered indexes](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes).

**CHECK/UNIQUE/FK xử lý được ở DB:** kiểu kênh/loại hợp lệ, tiền/lượng âm sai, FK, key trùng, quan hệ recipe XOR, nguồn movement XOR.

**Cần service + transaction/concurrency (không chỉ CHECK):** tổng dòng bằng tổng đơn, tổng hủy/pha không vượt số đặt, chỉ Customer sở hữu địa chỉ/đơn, Admin được duyệt, phiên bản hiệu lực không chồng, khoản ngân hàng đúng chiều, hạn mức hoàn, tổng giữ chỗ không âm, ledger khớp số dư. API luôn kiểm tra quyền ở mỗi lệnh.

`rowversion` giúp phát hiện ghi đè nhưng không tự bảo vệ tổng nhiều dòng hoặc toàn bộ hạn mức hoàn. Thiết kế transaction và unique keys vẫn bắt buộc.

## 8. Ánh xạ với 24 UC

| UC | Nhóm bảng chính |
|---|---|
| WEB-01/02 | UserAccounts, UserSessions, AuditLogs, IdempotencyRequests |
| WEB-03 | UserAccounts, CustomerAddresses |
| WEB-04 | Categories, Products, ProductVariants, Toppings, ProductToppings, Banners, BOM và tồn khả dụng |
| WEB-05 | Đầu tiên có thể giữ giỏ nháp ở trình duyệt; khi gửi đơn tạo OrderItems/Toppings/Ingredients snapshot. Không coi giỏ là đơn đã đặt. |
| WEB-06 | DiningTables, DeliveryRules/Quotes, Orders, OrderItems, IngredientReservations |
| WEB-07 | PaymentRequests, PaymentEvents, BankTransactions, Payments |
| WEB-08 | Orders, OrderStatusHistory, Cancellations/Items, Refunds/Attempts, Invoices/Lines |
| POS-01 | PosDevices, WorkSessions, CashMovements, AuditLogs |
| POS-02/03 | Orders, OrderItems, PreparationBatches, snapshot BOM, stock movements, OrderStatusHistory |
| POS-04 | Cancellations, CancellationItems, reservations, Refunds |
| POS-05 | Payments, CashMovements, Invoices, InvoiceLines, BankTransactions |
| ADM-01/02 | UserAccounts, UserSessions, Roles, UserRoles, Permissions, RolePermissions, PosDevices |
| ADM-03 | Categories, Products, ProductVariants, Toppings, ProductToppings, Banners |
| ADM-04/05 | Units, Ingredients, IngredientUnitConversions, Recipes, Versions, Ingredients, OptionAdjustments |
| ADM-06/07 | Warehouses, InventoryBalances/Documents/DocumentItems/Movements, reservations |
| ADM-08/09/10 | Orders/History, Invoices/Lines, Payments, Refunds/Attempts, Cancellations |
| ADM-11 | Truy vấn các bảng chứng từ/sổ giao dịch và AuditLogs; không tạo bảng “DoanhThu” cập nhật thủ công làm nguồn chân lý. |

Tên UC trong bảng được viết gọn; tiền tố đầy đủ là UC-WEB-, UC-POS-, UC-ADM- như đặc tả. Không bỏ Use Case khi chia nhóm.

## 9. Phân kỳ triển khai và điểm cần chốt

1. **Đợt 1:** master tài khoản/phiên, thiết bị/ca, menu, bàn, đơn + snapshot, PaymentRequests/Payments, invoices, audit/idempotency. Với chuyển khoản thật phải có inbox + giao dịch ngân hàng được xác minh, không chỉ tạo QR.
2. **Đợt 2:** BOM phiên bản, giữ chỗ/tiêu hao, chứng từ kho và hủy/hoàn có đối soát. Chỉ coi hệ thống đáp ứng đủ đặc tả khi hoàn thành nhóm này và test cạnh tranh.
3. **Đợt 3:** hoàn thiện outbox/reconnect, báo cáo/xuất, bảo vệ dữ liệu, vận hành và đối chiếu. Không thay bằng “đồng bộ” qua các UI tự đọc SQL.

Các điểm phải chốt trước DDL: quy tắc ship thực tế và hoàn phí, thời hạn giữ chỗ/thanh toán, mức đường/đá hỗ trợ và NET/GROSS BOM, quyền POS trên máy/khôi phục ca, tài liệu API nhận giao dịch ngân hàng, chính sách chứng từ và thời điểm ghi nhận doanh thu. Tài liệu này chọn trừ kho khi bắt đầu pha, đúng quy tắc đã thống nhất; không để hai thời điểm nhận đơn/hoàn tất đồng thời.

**Chưa thực thi:** không tạo CSDL, không sửa connection string, không cài EF Core, không chạy migration, không thay dữ liệu demo. Sau khi chốt mô hình có thể sinh SQL DDL/EF entities từ bản thiết kế này.
