# Sơ đồ quan hệ database đề xuất

Các bảng metadata/quyền/tích hợp được mô tả đầy đủ trong từ điển. Ba sơ đồ dưới đây tập trung vào các quan hệ nghiệp vụ chính; dấu `?` trong từ điển là FK nullable. Các quan hệ vẽ dưới đây dùng các FK bắt buộc, tránh hiểu mọi Order đều phải thuộc Customer vì POS cho khách lẻ.

## 1. Menu, đơn và pha chế

```mermaid
erDiagram
    Categories ||--o{ Products : contains
    Products ||--o{ ProductVariants : has
    Products ||--o{ ProductToppings : allows
    Toppings ||--o{ ProductToppings : allowed_for
    Orders ||--|{ OrderItems : contains
    ProductVariants ||--o{ OrderItems : selected_variant
    OrderItems ||--o{ OrderItemToppings : adds
    Toppings ||--o{ OrderItemToppings : selected
    RecipeVersions ||--o{ OrderItems : base_recipe_snapshot
    RecipeVersions ||--o{ OrderItemToppings : topping_recipe_snapshot
    OrderItems ||--|{ OrderItemIngredients : freezes_bom
    Ingredients ||--o{ OrderItemIngredients : required
    OrderItems ||--o{ PreparationBatches : prepares_parts
    WorkSessions ||--o{ PreparationBatches : starts
    Orders ||--o{ OrderStatusHistory : tracks
    Orders ||--o{ Cancellations : has
    Cancellations ||--o{ CancellationItems : cancels
    OrderItems ||--o{ CancellationItems : unstarted_quantity
```

## 2. Tài khoản, thiết bị và thu/hoàn

```mermaid
erDiagram
    UserAccounts ||--o{ UserSessions : has
    UserAccounts ||--o{ CustomerAddresses : saves
    UserAccounts ||--o{ UserRoles : assigned
    Roles ||--o{ UserRoles : granted
    Roles ||--o{ RolePermissions : includes
    Permissions ||--o{ RolePermissions : permission
    PosDevices ||--o{ WorkSessions : opens
    WorkSessions ||--o{ CashMovements : reconciles
    Orders ||--o{ PaymentRequests : requests
    Orders ||--o{ Payments : receives
    Orders ||--o{ Refunds : owes
    Refunds ||--o{ RefundAttempts : pays
    Orders ||--o{ Invoices : documents
    Invoices ||--|{ InvoiceLines : contains
```

`Orders.CustomerUserId` nullable đối với POS; `Orders.WorkSessionId` nullable đối với Web. `Payments.BankTransactionId` nullable vì có CASH; khoản CK được xác minh phải liên kết giao dịch tin cậy. `RefundAttempts.BankTransactionId` liên kết chi ngân hàng khi có; không gộp “duyệt” và “chi thành công” làm một bước.

## 3. Công thức và kho

```mermaid
erDiagram
    Recipes ||--o{ RecipeVersions : versions
    RecipeVersions ||--|{ RecipeIngredients : components
    RecipeVersions ||--o{ RecipeOptionAdjustments : sugar_ice
    Ingredients ||--o{ RecipeIngredients : ingredient
    Ingredients ||--o{ RecipeOptionAdjustments : replacement
    Units ||--o{ Ingredients : base_unit
    Ingredients ||--o{ IngredientUnitConversions : converts
    Warehouses ||--o{ InventoryBalances : balance
    Ingredients ||--o{ InventoryBalances : quantity
    Warehouses ||--o{ InventoryDocuments : documents
    InventoryDocuments ||--|{ InventoryDocumentItems : lines
    Ingredients ||--o{ InventoryDocumentItems : affects
    Warehouses ||--o{ InventoryMovements : ledger
    Ingredients ||--o{ InventoryMovements : moves
    OrderItems ||--o{ IngredientReservations : reserves
    Ingredients ||--o{ IngredientReservations : held
    Warehouses ||--o{ IngredientReservations : from_warehouse
```

`InventoryMovements` có đúng một nguồn trực tiếp: dòng chứng từ kho **hoặc** batch pha; dùng hai FK nullable + CHECK XOR và các unique index phù hợp. Công thức có đúng một chủ sở hữu: ProductVariant hoặc Topping. Hai quan hệ loại trừ này được diễn giải bằng ràng buộc trong từ điển thay vì vẽ như hai FK đều bắt buộc.

## 4. Luồng dữ liệu vận hành

```mermaid
flowchart LR
    UI[Web / POS / Admin] --> API[API kiểm tra quyền và version]
    API --> TX[Transaction: đơn / tiền / kho]
    TX --> AUDIT[AuditLogs]
    TX --> OUTBOX[OutboxMessages]
    OUTBOX --> WORKER[Worker phát sự kiện]
    WORKER --> RT[SignalR: tải trạng thái mới]
    RT --> UI
    BANK[Nguồn giao dịch ngân hàng] --> INBOX[PaymentEvents: xác minh và chống trùng]
    INBOX --> BANKTX[BankTransactions]
    BANKTX --> RECON[Đối soát vào Payments / RefundAttempts]
    RECON --> TX
```

Sơ đồ thể hiện mô hình đề xuất, chưa có API hoặc database thật được tạo trong dự án giao diện.
