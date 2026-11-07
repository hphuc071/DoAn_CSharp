-- REVIEW ONLY / NOT APPLIED. Independent of BR-17 EXPIRED; no automatic provisioning.
-- Caller must prove a NEW isolated container/port/database/schema. Name/approval alone is insufficient.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>0 THROW 51300, 'Dedicated migration session required.', 1;
IF DB_NAME()<>N'FourChangOutboxMigrationFixture'
    THROW 51301, 'Wrong migration target; no changes permitted.', 1;
IF ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N'ORDER_OUTBOX_MIGRATION_APPROVED')),0)<>1
    THROW 51302, 'Explicit migration approval required.', 1;
IF OBJECT_ID(N'dbo.OrderOutboxDelivery') IS NOT NULL
    THROW 51303, 'Delivery table already exists; stop for schema review.', 1;
IF OBJECT_ID(N'dbo.NhatKyHeThong',N'U') IS NULL
    THROW 51304, 'Expected immutable audit journal not found.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    CREATE TABLE dbo.OrderOutboxDelivery (
        AuditId bigint NOT NULL CONSTRAINT PK_OrderOutboxDelivery PRIMARY KEY,
        Status varchar(10) NOT NULL,
        Attempts int NOT NULL,
        NextAttemptUtc datetime2(3) NOT NULL,
        LeaseToken uniqueidentifier NULL,
        LeaseUntilUtc datetime2(3) NULL,
        DeliveredAtUtc datetime2(3) NULL,
        LastErrorCode varchar(50) NULL,
        CONSTRAINT FK_OrderOutboxDelivery_Audit FOREIGN KEY(AuditId) REFERENCES dbo.NhatKyHeThong(MaNhatKy),
        CONSTRAINT CK_OrderOutboxDelivery_Status CHECK(Status IN ('PENDING','LEASED','SENT','DEAD')),
        CONSTRAINT CK_OrderOutboxDelivery_Attempts CHECK(Attempts BETWEEN 0 AND 5),
        CONSTRAINT CK_OrderOutboxDelivery_Lease CHECK(
            (Status='LEASED' AND LeaseToken IS NOT NULL AND LeaseUntilUtc IS NOT NULL) OR
            (Status<>'LEASED' AND LeaseToken IS NULL AND LeaseUntilUtc IS NULL)),
        CONSTRAINT CK_OrderOutboxDelivery_Sent CHECK(
            (Status='SENT' AND DeliveredAtUtc IS NOT NULL) OR
            (Status<>'SENT' AND DeliveredAtUtc IS NULL))
    );
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
