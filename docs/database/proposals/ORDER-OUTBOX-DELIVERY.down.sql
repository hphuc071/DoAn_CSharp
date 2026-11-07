-- REVIEW ONLY / NOT APPLIED. Prefer disabling dispatcher and retaining delivery evidence.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>0 THROW 51300, 'Dedicated migration session required.', 1;
IF DB_NAME()<>N'FourChangOutboxMigrationFixture'
    THROW 51301, 'Wrong migration target; no changes permitted.', 1;
IF ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N'ORDER_OUTBOX_ROLLBACK_APPROVED')),0)<>1
    THROW 51302, 'Explicit rollback approval required.', 1;
IF OBJECT_ID(N'dbo.OrderOutboxDelivery',N'U') IS NULL
    THROW 51303, 'Expected delivery table not found.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS(SELECT 1 FROM dbo.OrderOutboxDelivery WITH(UPDLOCK,HOLDLOCK))
        THROW 51305, 'Delivery history exists; retain it and forward-fix. No automatic deletion.', 1;
    DROP TABLE dbo.OrderOutboxDelivery;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
