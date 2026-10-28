/*
  SQL Server 2019+ - thiet ke de xuat cho 24 use case.
  Chay trong database MOI, RONG do ban tu tao/chon trong SSMS.
  Script khong tao/xoa database va khong chua du lieu mau.
  Thoi gian luu UTC. So tien: VND. So luong kho: don vi co so.
  Vi du: CREATE DATABASE MocCafe; sau do chon MocCafe va chay file nay.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

-- 1. TAI KHOAN VA VAN HANH
CREATE TABLE dbo.Customers (
    CustomerId bigint IDENTITY PRIMARY KEY,
    FullName nvarchar(120) NOT NULL,
    Email varchar(254) NULL,
    Phone varchar(20) NULL,
    PasswordHash varchar(512) NOT NULL,
    IsLocked bit NOT NULL DEFAULT 0,
    MustChangePassword bit NOT NULL DEFAULT 0,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Version rowversion,
    CONSTRAINT CK_Customers_Contact CHECK (Email IS NOT NULL OR Phone IS NOT NULL)
);
CREATE UNIQUE INDEX UX_Customers_Email ON dbo.Customers(Email) WHERE Email IS NOT NULL;
CREATE UNIQUE INDEX UX_Customers_Phone ON dbo.Customers(Phone) WHERE Phone IS NOT NULL;

CREATE TABLE dbo.CustomerAddresses (
    AddressId bigint IDENTITY PRIMARY KEY,
    CustomerId bigint NOT NULL REFERENCES dbo.Customers(CustomerId),
    RecipientName nvarchar(120) NOT NULL,
    Phone varchar(20) NOT NULL,
    AddressText nvarchar(500) NOT NULL,
    Latitude decimal(9,6) NULL,
    Longitude decimal(9,6) NULL,
    IsDefault bit NOT NULL DEFAULT 0,
    CONSTRAINT CK_Address_Lat CHECK (Latitude BETWEEN -90 AND 90),
    CONSTRAINT CK_Address_Lon CHECK (Longitude BETWEEN -180 AND 180)
);
CREATE UNIQUE INDEX UX_Address_Default ON dbo.CustomerAddresses(CustomerId) WHERE IsDefault = 1;

CREATE TABLE dbo.AdminAccounts (
    AdminId bigint IDENTITY PRIMARY KEY,
    UserName varchar(80) NOT NULL UNIQUE,
    FullName nvarchar(120) NOT NULL,
    PasswordHash varchar(512) NOT NULL,
    IsLocked bit NOT NULL DEFAULT 0,
    MustChangePassword bit NOT NULL DEFAULT 0,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Version rowversion
);
CREATE TABLE dbo.Roles (
    RoleId int IDENTITY PRIMARY KEY,
    RoleCode varchar(50) NOT NULL UNIQUE,
    RoleName nvarchar(100) NOT NULL
);
CREATE TABLE dbo.Permissions (
    PermissionId int IDENTITY PRIMARY KEY,
    PermissionCode varchar(100) NOT NULL UNIQUE,
    Description nvarchar(250) NOT NULL
);
CREATE TABLE dbo.AdminRoles (
    AdminId bigint NOT NULL REFERENCES dbo.AdminAccounts(AdminId),
    RoleId int NOT NULL REFERENCES dbo.Roles(RoleId),
    PRIMARY KEY (AdminId, RoleId)
);
CREATE TABLE dbo.RolePermissions (
    RoleId int NOT NULL REFERENCES dbo.Roles(RoleId),
    PermissionId int NOT NULL REFERENCES dbo.Permissions(PermissionId),
    PRIMARY KEY (RoleId, PermissionId)
);
CREATE TABLE dbo.PosTerminals (
    TerminalId int IDENTITY PRIMARY KEY,
    TerminalCode varchar(30) NOT NULL UNIQUE,
    TerminalName nvarchar(100) NOT NULL,
    CredentialHash varchar(512) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    RegisteredBy bigint NOT NULL REFERENCES dbo.AdminAccounts(AdminId),
    RegisteredAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    RevokedAt datetime2 NULL,
    Version rowversion
);
CREATE TABLE dbo.Shifts (
    ShiftId bigint IDENTITY PRIMARY KEY,
    TerminalId int NOT NULL REFERENCES dbo.PosTerminals(TerminalId),
    StaffName nvarchar(120) NOT NULL,
    OpeningCash decimal(18,0) NOT NULL,
    ExpectedClosingCash decimal(18,0) NULL,
    ActualClosingCash decimal(18,0) NULL,
    CashDifference AS (ActualClosingCash - ExpectedClosingCash),
    Status varchar(20) NOT NULL DEFAULT 'OPEN',
    OpenedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ClosedAt datetime2 NULL,
    Version rowversion,
    CONSTRAINT CK_Shifts_Status CHECK (Status IN ('OPEN','LOCKED','CLOSED')),
    CONSTRAINT CK_Shifts_Cash CHECK (OpeningCash >= 0 AND (ActualClosingCash IS NULL OR ActualClosingCash >= 0)),
    CONSTRAINT CK_Shifts_Close CHECK ((Status = 'CLOSED' AND ClosedAt IS NOT NULL AND ActualClosingCash IS NOT NULL AND ExpectedClosingCash IS NOT NULL) OR (Status <> 'CLOSED' AND ClosedAt IS NULL)),
    CONSTRAINT CK_Shifts_Time CHECK (ClosedAt IS NULL OR ClosedAt >= OpenedAt)
);
-- Mot may chi co mot ca chua dong (ke ca ca dang khoa).
CREATE UNIQUE INDEX UX_Shifts_Active ON dbo.Shifts(TerminalId) WHERE ClosedAt IS NULL;
CREATE TABLE dbo.ShiftHandovers (
    HandoverId bigint IDENTITY PRIMARY KEY,
    ShiftId bigint NOT NULL REFERENCES dbo.Shifts(ShiftId),
    FromStaffName nvarchar(120) NOT NULL,
    ToStaffName nvarchar(120) NOT NULL,
    CashCounted decimal(18,0) NOT NULL CHECK (CashCounted >= 0),
    Reason nvarchar(500) NOT NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE TABLE dbo.DiningTables (
    TableId int IDENTITY PRIMARY KEY,
    TableCode varchar(20) NOT NULL UNIQUE,
    TableName nvarchar(80) NOT NULL,
    QrTokenHash varchar(128) NULL,
    QrExpiresAt datetime2 NULL,
    IsActive bit NOT NULL DEFAULT 1
);

-- 2. THUC DON, TUY CHON VA CONG THUC
CREATE TABLE dbo.Categories (
    CategoryId int IDENTITY PRIMARY KEY,
    CategoryName nvarchar(100) NOT NULL UNIQUE,
    SortOrder int NOT NULL DEFAULT 0,
    IsActive bit NOT NULL DEFAULT 1
);
CREATE TABLE dbo.Products (
    ProductId bigint IDENTITY PRIMARY KEY,
    CategoryId int NOT NULL REFERENCES dbo.Categories(CategoryId),
    ProductCode varchar(30) NOT NULL UNIQUE,
    ProductName nvarchar(150) NOT NULL,
    Description nvarchar(1000) NULL,
    ImageUrl nvarchar(500) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    Version rowversion
);
CREATE TABLE dbo.ProductVariants (
    VariantId bigint IDENTITY PRIMARY KEY,
    ProductId bigint NOT NULL REFERENCES dbo.Products(ProductId),
    SizeCode varchar(10) NOT NULL,
    Temperature varchar(10) NOT NULL,
    Price decimal(18,0) NOT NULL CHECK (Price >= 0),
    IsActive bit NOT NULL DEFAULT 1,
    Version rowversion,
    CONSTRAINT UQ_Variants UNIQUE (ProductId, SizeCode, Temperature),
    CONSTRAINT CK_Variants_Temp CHECK (Temperature IN ('HOT','COLD','NA'))
);
-- Duong, da, topping deu la lua chon co cau truc, khong suy ra tu ghi chu.
CREATE TABLE dbo.OptionGroups (
    OptionGroupId int IDENTITY PRIMARY KEY,
    GroupCode varchar(30) NOT NULL UNIQUE,
    GroupName nvarchar(100) NOT NULL,
    SelectionMode varchar(10) NOT NULL CHECK (SelectionMode IN ('SINGLE','MULTIPLE'))
);
CREATE TABLE dbo.OptionValues (
    OptionValueId bigint IDENTITY PRIMARY KEY,
    OptionGroupId int NOT NULL REFERENCES dbo.OptionGroups(OptionGroupId),
    ValueName nvarchar(100) NOT NULL,
    ExtraPrice decimal(18,0) NOT NULL DEFAULT 0 CHECK (ExtraPrice >= 0),
    IsActive bit NOT NULL DEFAULT 1,
    CONSTRAINT UQ_OptionValues UNIQUE (OptionGroupId, ValueName)
);
CREATE TABLE dbo.VariantOptions (
    VariantId bigint NOT NULL REFERENCES dbo.ProductVariants(VariantId),
    OptionValueId bigint NOT NULL REFERENCES dbo.OptionValues(OptionValueId),
    IsDefault bit NOT NULL DEFAULT 0,
    PRIMARY KEY (VariantId, OptionValueId)
);
CREATE TABLE dbo.Banners (
    BannerId int IDENTITY PRIMARY KEY,
    Title nvarchar(150) NOT NULL,
    ImageUrl nvarchar(500) NOT NULL,
    TargetUrl nvarchar(500) NULL,
    StartsAt datetime2 NOT NULL,
    EndsAt datetime2 NULL,
    SortOrder int NOT NULL DEFAULT 0,
    IsActive bit NOT NULL DEFAULT 1,
    CONSTRAINT CK_Banner_Time CHECK (EndsAt IS NULL OR EndsAt > StartsAt)
);
CREATE TABLE dbo.Ingredients (
    IngredientId bigint IDENTITY PRIMARY KEY,
    IngredientCode varchar(30) NOT NULL UNIQUE,
    IngredientName nvarchar(150) NOT NULL,
    BaseUnit nvarchar(20) NOT NULL,
    SafetyStock decimal(18,4) NOT NULL DEFAULT 0 CHECK (SafetyStock >= 0),
    LowStockThreshold decimal(18,4) NOT NULL DEFAULT 0 CHECK (LowStockThreshold >= 0),
    IsActive bit NOT NULL DEFAULT 1,
    Version rowversion
);
CREATE TABLE dbo.IngredientUnits (
    IngredientUnitId bigint IDENTITY PRIMARY KEY,
    IngredientId bigint NOT NULL REFERENCES dbo.Ingredients(IngredientId),
    UnitName nvarchar(30) NOT NULL,
    BaseFactor decimal(18,6) NOT NULL CHECK (BaseFactor > 0),
    CONSTRAINT UQ_IngredientUnits UNIQUE (IngredientId, UnitName)
);
CREATE TABLE dbo.Recipes (
    RecipeId bigint IDENTITY PRIMARY KEY,
    VariantId bigint NOT NULL REFERENCES dbo.ProductVariants(VariantId),
    VersionNumber int NOT NULL CHECK (VersionNumber > 0),
    Status varchar(15) NOT NULL DEFAULT 'DRAFT' CHECK (Status IN ('DRAFT','ACTIVE','RETIRED')),
    EffectiveFrom datetime2 NOT NULL,
    EffectiveTo datetime2 NULL,
    CreatedBy bigint NOT NULL REFERENCES dbo.AdminAccounts(AdminId),
    CONSTRAINT UQ_Recipes_Version UNIQUE (VariantId, VersionNumber),
    CONSTRAINT UQ_Recipes_Variant UNIQUE (RecipeId, VariantId),
    CONSTRAINT CK_Recipes_Time CHECK (EffectiveTo IS NULL OR EffectiveTo > EffectiveFrom)
);
CREATE UNIQUE INDEX UX_Recipes_Active ON dbo.Recipes(VariantId) WHERE Status = 'ACTIVE';
CREATE TABLE dbo.RecipeLines (
    RecipeId bigint NOT NULL REFERENCES dbo.Recipes(RecipeId),
    IngredientId bigint NOT NULL REFERENCES dbo.Ingredients(IngredientId),
    BaseQuantity decimal(18,4) NOT NULL CHECK (BaseQuantity > 0),
    PRIMARY KEY (RecipeId, IngredientId)
);
-- Dieu chinh dinh luong theo lua chon: so am giam, so duong tang so voi BOM goc.
CREATE TABLE dbo.RecipeOptionAdjustments (
    RecipeId bigint NOT NULL REFERENCES dbo.Recipes(RecipeId),
    OptionValueId bigint NOT NULL REFERENCES dbo.OptionValues(OptionValueId),
    IngredientId bigint NOT NULL REFERENCES dbo.Ingredients(IngredientId),
    QuantityDelta decimal(18,4) NOT NULL CHECK (QuantityDelta <> 0),
    PRIMARY KEY (RecipeId, OptionValueId, IngredientId)
);

-- 3. DON HANG: LUONG DA PHA VA LUONG HUY DUOC THEO DOI RIENG
CREATE TABLE dbo.Orders (
    OrderId bigint IDENTITY PRIMARY KEY,
    OrderCode varchar(30) NOT NULL UNIQUE,
    CustomerId bigint NULL REFERENCES dbo.Customers(CustomerId),
    ShiftId bigint NULL REFERENCES dbo.Shifts(ShiftId),
    Channel varchar(10) NOT NULL CHECK (Channel IN ('WEB','POS')),
    FulfillmentType varchar(15) NOT NULL CHECK (FulfillmentType IN ('DINE_IN','TAKEAWAY','DELIVERY')),
    TableId int NULL REFERENCES dbo.DiningTables(TableId),
    RecipientName nvarchar(120) NULL,
    RecipientPhone varchar(20) NULL,
    DeliveryAddressSnapshot nvarchar(500) NULL,
    Latitude decimal(9,6) NULL CHECK (Latitude BETWEEN -90 AND 90),
    Longitude decimal(9,6) NULL CHECK (Longitude BETWEEN -180 AND 180),
    RequestedAt datetime2 NULL,
    Status varchar(25) NOT NULL DEFAULT 'PENDING_PAYMENT',
    Subtotal decimal(18,0) NOT NULL CHECK (Subtotal >= 0),
    DeliveryFee decimal(18,0) NOT NULL DEFAULT 0 CHECK (DeliveryFee >= 0),
    TotalAmount AS (Subtotal + DeliveryFee) PERSISTED,
    Note nvarchar(500) NULL,
    CancelReason nvarchar(500) NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Version rowversion,
    CONSTRAINT CK_Orders_Status CHECK (Status IN ('DRAFT','PENDING_PAYMENT','PENDING_CONFIRMATION','ACCEPTED','PREPARING','READY','DELIVERING','COMPLETED','REJECTED','CANCELLED','DELIVERY_FAILED')),
    CONSTRAINT CK_Orders_Origin CHECK ((Channel = 'WEB' AND CustomerId IS NOT NULL) OR (Channel = 'POS' AND ShiftId IS NOT NULL)),
    CONSTRAINT CK_Orders_Table CHECK ((FulfillmentType = 'DINE_IN' AND TableId IS NOT NULL) OR (FulfillmentType <> 'DINE_IN' AND TableId IS NULL)),
    CONSTRAINT CK_Orders_Delivery CHECK (FulfillmentType <> 'DELIVERY' OR (RecipientName IS NOT NULL AND RecipientPhone IS NOT NULL AND DeliveryAddressSnapshot IS NOT NULL))
);
CREATE TABLE dbo.OrderItems (
    OrderItemId bigint IDENTITY PRIMARY KEY,
    OrderId bigint NOT NULL REFERENCES dbo.Orders(OrderId),
    VariantId bigint NOT NULL REFERENCES dbo.ProductVariants(VariantId),
    RecipeId bigint NOT NULL,
    ProductNameSnapshot nvarchar(150) NOT NULL,
    SizeSnapshot varchar(10) NOT NULL,
    TemperatureSnapshot varchar(10) NOT NULL,
    BasePriceSnapshot decimal(18,0) NOT NULL CHECK (BasePriceSnapshot >= 0),
    OptionsPriceSnapshot decimal(18,0) NOT NULL DEFAULT 0 CHECK (OptionsPriceSnapshot >= 0),
    UnitPrice AS (BasePriceSnapshot + OptionsPriceSnapshot) PERSISTED,
    Quantity int NOT NULL CHECK (Quantity > 0),
    StartedQuantity int NOT NULL DEFAULT 0 CHECK (StartedQuantity >= 0),
    CompletedQuantity int NOT NULL DEFAULT 0 CHECK (CompletedQuantity >= 0),
    CancelledQuantity int NOT NULL DEFAULT 0 CHECK (CancelledQuantity >= 0),
    Note nvarchar(500) NULL,
    Version rowversion,
    CONSTRAINT FK_OrderItem_Recipe FOREIGN KEY (RecipeId, VariantId) REFERENCES dbo.Recipes(RecipeId, VariantId),
    CONSTRAINT CK_OrderItem_Quantities CHECK (StartedQuantity + CancelledQuantity <= Quantity AND CompletedQuantity <= StartedQuantity)
);
CREATE TABLE dbo.OrderItemOptions (
    OrderItemId bigint NOT NULL REFERENCES dbo.OrderItems(OrderItemId),
    OptionValueId bigint NOT NULL REFERENCES dbo.OptionValues(OptionValueId),
    GroupNameSnapshot nvarchar(100) NOT NULL,
    ValueNameSnapshot nvarchar(100) NOT NULL,
    ExtraPriceSnapshot decimal(18,0) NOT NULL CHECK (ExtraPriceSnapshot >= 0),
    PRIMARY KEY (OrderItemId, OptionValueId)
);
-- BOM da tinh tu tat ca lua chon cho MOT don vi mon; giu nguyen sau khi dat.
CREATE TABLE dbo.OrderItemIngredients (
    OrderItemId bigint NOT NULL REFERENCES dbo.OrderItems(OrderItemId),
    IngredientId bigint NOT NULL REFERENCES dbo.Ingredients(IngredientId),
    QuantityPerItem decimal(18,4) NOT NULL CHECK (QuantityPerItem > 0),
    PRIMARY KEY (OrderItemId, IngredientId)
);
CREATE TABLE dbo.OrderCancellations (
    CancellationId bigint IDENTITY PRIMARY KEY,
    OrderItemId bigint NOT NULL REFERENCES dbo.OrderItems(OrderItemId),
    Quantity int NOT NULL CHECK (Quantity > 0),
    Reason nvarchar(500) NOT NULL,
    RequestedByCustomerId bigint NULL REFERENCES dbo.Customers(CustomerId),
    ShiftId bigint NULL REFERENCES dbo.Shifts(ShiftId),
    AdminId bigint NULL REFERENCES dbo.AdminAccounts(AdminId),
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Cancellation_Actor CHECK (
        (CASE WHEN RequestedByCustomerId IS NULL THEN 0 ELSE 1 END +
         CASE WHEN ShiftId IS NULL THEN 0 ELSE 1 END +
         CASE WHEN AdminId IS NULL THEN 0 ELSE 1 END) = 1)
);
CREATE TABLE dbo.OrderStatusHistory (
    HistoryId bigint IDENTITY PRIMARY KEY,
    OrderId bigint NOT NULL REFERENCES dbo.Orders(OrderId),
    FromStatus varchar(25) NULL,
    ToStatus varchar(25) NOT NULL,
    Reason nvarchar(500) NULL,
    ShiftId bigint NULL REFERENCES dbo.Shifts(ShiftId),
    AdminId bigint NULL REFERENCES dbo.AdminAccounts(AdminId),
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- 4. THANH TOAN, HOA DON VA HOAN TIEN
CREATE TABLE dbo.Payments (
    PaymentId bigint IDENTITY PRIMARY KEY,
    OrderId bigint NOT NULL REFERENCES dbo.Orders(OrderId),
    ShiftId bigint NULL REFERENCES dbo.Shifts(ShiftId),
    Method varchar(10) NOT NULL CHECK (Method IN ('CASH','TRANSFER')),
    ExpectedAmount decimal(18,0) NOT NULL CHECK (ExpectedAmount >= 0),
    VerifiedAmount decimal(18,0) NOT NULL DEFAULT 0 CHECK (VerifiedAmount >= 0),
    CashTendered decimal(18,0) NULL,
    CashChange decimal(18,0) NULL,
    TransferReference varchar(100) NULL,
    Status varchar(20) NOT NULL DEFAULT 'PENDING' CHECK (Status IN ('PENDING','VERIFIED','UNDERPAID','OVERPAID','EXPIRED','RECONCILING','FAILED')),
    ExpiresAt datetime2 NULL,
    VerifiedAt datetime2 NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Version rowversion,
    CONSTRAINT CK_Payments_Cash CHECK ((Method = 'TRANSFER' AND CashTendered IS NULL AND CashChange IS NULL) OR (Method = 'CASH' AND ShiftId IS NOT NULL AND CashTendered IS NOT NULL AND CashChange IS NOT NULL AND CashTendered >= ExpectedAmount AND CashChange = CashTendered - ExpectedAmount))
);
CREATE UNIQUE INDEX UX_Payment_Reference ON dbo.Payments(TransferReference) WHERE TransferReference IS NOT NULL;
-- Chi luu giao dich ngan hang da tiep nhan; khong coi anh QR la bang chung da thu.
CREATE TABLE dbo.BankTransactions (
    BankTransactionId bigint IDENTITY PRIMARY KEY,
    Provider varchar(50) NOT NULL,
    ExternalTransactionId varchar(150) NOT NULL,
    PaymentId bigint NULL REFERENCES dbo.Payments(PaymentId),
    Amount decimal(18,0) NOT NULL CHECK (Amount > 0),
    TransferContent nvarchar(500) NULL,
    ReceivedAt datetime2 NOT NULL,
    RecordedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_BankTransaction UNIQUE (Provider, ExternalTransactionId)
);
CREATE TABLE dbo.Invoices (
    InvoiceId bigint IDENTITY PRIMARY KEY,
    InvoiceNumber varchar(40) NOT NULL UNIQUE,
    OrderId bigint NOT NULL REFERENCES dbo.Orders(OrderId),
    InvoiceType varchar(15) NOT NULL CHECK (InvoiceType IN ('ORIGINAL','ADJUSTMENT')),
    OriginalInvoiceId bigint NULL REFERENCES dbo.Invoices(InvoiceId),
    Amount decimal(18,0) NOT NULL,
    Reason nvarchar(500) NULL,
    DocumentSnapshot nvarchar(max) NOT NULL CHECK (ISJSON(DocumentSnapshot) = 1),
    IssuedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Invoices_Type CHECK ((InvoiceType = 'ORIGINAL' AND OriginalInvoiceId IS NULL AND Amount >= 0) OR (InvoiceType = 'ADJUSTMENT' AND OriginalInvoiceId IS NOT NULL AND Reason IS NOT NULL))
);
CREATE UNIQUE INDEX UX_Invoices_Original ON dbo.Invoices(OrderId) WHERE InvoiceType = 'ORIGINAL';
CREATE TABLE dbo.Refunds (
    RefundId bigint IDENTITY PRIMARY KEY,
    RefundCode varchar(40) NOT NULL UNIQUE,
    PaymentId bigint NOT NULL REFERENCES dbo.Payments(PaymentId),
    CancellationId bigint NULL REFERENCES dbo.OrderCancellations(CancellationId),
    Amount decimal(18,0) NOT NULL CHECK (Amount > 0),
    Reason nvarchar(500) NOT NULL,
    Status varchar(20) NOT NULL DEFAULT 'REQUESTED' CHECK (Status IN ('REQUESTED','APPROVED','PROCESSING','SUCCEEDED','FAILED','RECONCILING','REJECTED')),
    RecipientName nvarchar(120) NULL,
    RecipientBank nvarchar(100) NULL,
    RecipientAccount varchar(50) NULL,
    ApprovedBy bigint NULL REFERENCES dbo.AdminAccounts(AdminId),
    ApprovedAt datetime2 NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Version rowversion
);
CREATE TABLE dbo.RefundAttempts (
    RefundAttemptId bigint IDENTITY PRIMARY KEY,
    RefundId bigint NOT NULL REFERENCES dbo.Refunds(RefundId),
    AttemptKey uniqueidentifier NOT NULL UNIQUE,
    Provider varchar(50) NOT NULL,
    ExternalReference varchar(150) NULL,
    Status varchar(20) NOT NULL CHECK (Status IN ('PENDING','SUCCEEDED','FAILED','RECONCILING')),
    EvidenceUrl nvarchar(500) NULL,
    VerifiedBy bigint NULL REFERENCES dbo.AdminAccounts(AdminId),
    VerifiedAt datetime2 NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_RefundAttempt_External ON dbo.RefundAttempts(Provider, ExternalReference) WHERE ExternalReference IS NOT NULL;
CREATE UNIQUE INDEX UX_RefundAttempt_Success ON dbo.RefundAttempts(RefundId) WHERE Status = 'SUCCEEDED';

-- 5. KHO: TON, GIU CHO, CHUNG TU VA SO KHO
CREATE TABLE dbo.Suppliers (
    SupplierId bigint IDENTITY PRIMARY KEY,
    SupplierName nvarchar(150) NOT NULL,
    Phone varchar(20) NULL,
    AddressText nvarchar(500) NULL,
    IsActive bit NOT NULL DEFAULT 1
);
CREATE TABLE dbo.StockDocuments (
    StockDocumentId bigint IDENTITY PRIMARY KEY,
    DocumentCode varchar(40) NOT NULL UNIQUE,
    DocumentType varchar(15) NOT NULL CHECK (DocumentType IN ('RECEIPT','ISSUE','STOCKTAKE','REVERSAL')),
    SupplierId bigint NULL REFERENCES dbo.Suppliers(SupplierId),
    ReversesDocumentId bigint NULL REFERENCES dbo.StockDocuments(StockDocumentId),
    Status varchar(10) NOT NULL DEFAULT 'DRAFT' CHECK (Status IN ('DRAFT','POSTED')),
    Reason nvarchar(500) NULL,
    CreatedBy bigint NOT NULL REFERENCES dbo.AdminAccounts(AdminId),
    ApprovedBy bigint NULL REFERENCES dbo.AdminAccounts(AdminId),
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    PostedAt datetime2 NULL,
    Version rowversion,
    CONSTRAINT CK_StockDoc_Posted CHECK ((Status = 'DRAFT' AND PostedAt IS NULL) OR (Status = 'POSTED' AND PostedAt IS NOT NULL AND ApprovedBy IS NOT NULL)),
    CONSTRAINT CK_StockDoc_Reversal CHECK ((DocumentType = 'REVERSAL' AND ReversesDocumentId IS NOT NULL AND Reason IS NOT NULL) OR (DocumentType <> 'REVERSAL' AND ReversesDocumentId IS NULL))
);
CREATE UNIQUE INDEX UX_StockDoc_Reversed ON dbo.StockDocuments(ReversesDocumentId) WHERE ReversesDocumentId IS NOT NULL;
CREATE TABLE dbo.StockDocumentLines (
    StockDocumentLineId bigint IDENTITY PRIMARY KEY,
    StockDocumentId bigint NOT NULL REFERENCES dbo.StockDocuments(StockDocumentId),
    IngredientId bigint NOT NULL REFERENCES dbo.Ingredients(IngredientId),
    InputUnitSnapshot nvarchar(30) NOT NULL,
    ConversionFactorSnapshot decimal(18,6) NOT NULL CHECK (ConversionFactorSnapshot > 0),
    InputQuantity decimal(18,4) NOT NULL CHECK (InputQuantity >= 0),
    BaseQuantityDelta decimal(18,4) NOT NULL,
    UnitCost decimal(18,2) NULL CHECK (UnitCost >= 0),
    ExpectedQuantitySnapshot decimal(18,4) NULL,
    CountedQuantity decimal(18,4) NULL CHECK (CountedQuantity >= 0),
    InventoryVersionSnapshot binary(8) NULL,
    CONSTRAINT UQ_StockDoc_Ingredient UNIQUE (StockDocumentId, IngredientId)
);
CREATE TABLE dbo.InventoryBalances (
    IngredientId bigint PRIMARY KEY REFERENCES dbo.Ingredients(IngredientId),
    OnHandQuantity decimal(18,4) NOT NULL DEFAULT 0 CHECK (OnHandQuantity >= 0),
    ReservedQuantity decimal(18,4) NOT NULL DEFAULT 0 CHECK (ReservedQuantity >= 0),
    UpdatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Version rowversion,
    CONSTRAINT CK_Inventory_Reserved CHECK (ReservedQuantity <= OnHandQuantity)
);
CREATE TABLE dbo.InventoryReservations (
    OrderItemId bigint NOT NULL,
    IngredientId bigint NOT NULL,
    ReservedQuantity decimal(18,4) NOT NULL CHECK (ReservedQuantity >= 0),
    ConsumedQuantity decimal(18,4) NOT NULL DEFAULT 0 CHECK (ConsumedQuantity >= 0),
    ReleasedQuantity decimal(18,4) NOT NULL DEFAULT 0 CHECK (ReleasedQuantity >= 0),
    RemainingQuantity AS (ReservedQuantity - ConsumedQuantity - ReleasedQuantity) PERSISTED,
    Version rowversion,
    PRIMARY KEY (OrderItemId, IngredientId),
    CONSTRAINT FK_Reservation_ItemIngredient FOREIGN KEY (OrderItemId, IngredientId) REFERENCES dbo.OrderItemIngredients(OrderItemId, IngredientId),
    CONSTRAINT CK_Reservation_Quantities CHECK (ConsumedQuantity + ReleasedQuantity <= ReservedQuantity)
);
CREATE TABLE dbo.StockMovements (
    MovementId bigint IDENTITY PRIMARY KEY,
    IngredientId bigint NOT NULL REFERENCES dbo.Ingredients(IngredientId),
    StockDocumentLineId bigint NULL REFERENCES dbo.StockDocumentLines(StockDocumentLineId),
    OrderItemId bigint NULL REFERENCES dbo.OrderItems(OrderItemId),
    QuantityDelta decimal(18,4) NOT NULL CHECK (QuantityDelta <> 0),
    OperationKey uniqueidentifier NOT NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_StockMovement_Operation UNIQUE (OperationKey, IngredientId),
    CONSTRAINT CK_StockMovement_Source CHECK ((StockDocumentLineId IS NOT NULL AND OrderItemId IS NULL) OR (StockDocumentLineId IS NULL AND OrderItemId IS NOT NULL AND QuantityDelta < 0))
);
CREATE UNIQUE INDEX UX_Movement_DocumentLine ON dbo.StockMovements(StockDocumentLineId) WHERE StockDocumentLineId IS NOT NULL;

-- 6. CHONG GUI LAP VA NHAT KY
CREATE TABLE dbo.CommandRequests (
    RequestKey uniqueidentifier PRIMARY KEY,
    CommandName varchar(100) NOT NULL,
    ActorKey varchar(100) NOT NULL,
    PayloadHash binary(32) NOT NULL,
    Status varchar(15) NOT NULL CHECK (Status IN ('PROCESSING','SUCCEEDED','FAILED','RECONCILING')),
    ResultJson nvarchar(max) NULL CHECK (ResultJson IS NULL OR ISJSON(ResultJson) = 1),
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAt datetime2 NULL
);
CREATE TABLE dbo.AuditLogs (
    AuditId bigint IDENTITY PRIMARY KEY,
    CorrelationId uniqueidentifier NOT NULL,
    ActorType varchar(15) NOT NULL CHECK (ActorType IN ('CUSTOMER','ADMIN','POS','SYSTEM')),
    CustomerId bigint NULL REFERENCES dbo.Customers(CustomerId),
    AdminId bigint NULL REFERENCES dbo.AdminAccounts(AdminId),
    ShiftId bigint NULL REFERENCES dbo.Shifts(ShiftId),
    Action varchar(100) NOT NULL,
    EntityType varchar(80) NOT NULL,
    EntityId varchar(80) NOT NULL,
    BeforeJson nvarchar(max) NULL CHECK (BeforeJson IS NULL OR ISJSON(BeforeJson) = 1),
    AfterJson nvarchar(max) NULL CHECK (AfterJson IS NULL OR ISJSON(AfterJson) = 1),
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Audit_Actor CHECK (
        (ActorType = 'CUSTOMER' AND CustomerId IS NOT NULL AND AdminId IS NULL AND ShiftId IS NULL) OR
        (ActorType = 'ADMIN' AND AdminId IS NOT NULL AND CustomerId IS NULL AND ShiftId IS NULL) OR
        (ActorType = 'POS' AND ShiftId IS NOT NULL AND CustomerId IS NULL AND AdminId IS NULL) OR
        (ActorType = 'SYSTEM' AND CustomerId IS NULL AND AdminId IS NULL AND ShiftId IS NULL))
);

CREATE INDEX IX_Orders_Customer_Date ON dbo.Orders(CustomerId, CreatedAt DESC);
CREATE INDEX IX_Orders_Status_Date ON dbo.Orders(Status, CreatedAt DESC);
CREATE INDEX IX_Orders_Shift ON dbo.Orders(ShiftId);
CREATE INDEX IX_OrderItems_Order ON dbo.OrderItems(OrderId);
CREATE INDEX IX_Payments_Order ON dbo.Payments(OrderId);
CREATE INDEX IX_Refunds_Payment ON dbo.Refunds(PaymentId);
CREATE INDEX IX_StatusHistory_Order ON dbo.OrderStatusHistory(OrderId, CreatedAt);
CREATE INDEX IX_StockMovements_Ingredient ON dbo.StockMovements(IngredientId, CreatedAt);
CREATE INDEX IX_Audit_Correlation ON dbo.AuditLogs(CorrelationId);
CREATE INDEX IX_Audit_Entity ON dbo.AuditLogs(EntityType, EntityId, CreatedAt);

COMMIT TRANSACTION;
GO
CREATE VIEW dbo.vw_InventoryAvailability
AS
SELECT i.IngredientId, i.IngredientCode, i.IngredientName, i.BaseUnit,
       COALESCE(b.OnHandQuantity, 0) AS OnHandQuantity,
       COALESCE(b.ReservedQuantity, 0) AS ReservedQuantity,
       i.SafetyStock,
       COALESCE(b.OnHandQuantity, 0) - COALESCE(b.ReservedQuantity, 0) - i.SafetyStock AS AvailableQuantity,
       i.LowStockThreshold
FROM dbo.Ingredients i
LEFT JOIN dbo.InventoryBalances b ON b.IngredientId = i.IngredientId;
GO
