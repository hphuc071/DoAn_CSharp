-- REVIEW ONLY / NOT APPLIED. Requires separate BR-17 and migration approval.
-- Review target only: dedicated future FourChangBr17MigrationFixture, not historical/test fixtures.
-- Container/port/identity and provisioning need separate review; database name alone is not isolation.
SET XACT_ABORT ON;
IF @@TRANCOUNT <> 0 THROW 51204, 'Run only in a dedicated migration session.', 1;
IF DB_NAME() <> N'FourChangBr17MigrationFixture'
    THROW 51200, 'Wrong database. No schema changes permitted.', 1;
IF TRY_CONVERT(int, SESSION_CONTEXT(N'BR17_MIGRATION_APPROVED')) <> 1
   OR SESSION_CONTEXT(N'BR17_MIGRATION_APPROVED') IS NULL
    THROW 51201, 'BR17 migration approval is required; this is a review artifact.', 1;
-- Reject schema drift instead of silently replacing a different trusted constraint.
-- Accept SQL Server's normalized OR form or IN form, exactly the reviewed token set.
DECLARE @definition nvarchar(max) = (SELECT definition FROM sys.check_constraints
    WHERE parent_object_id=OBJECT_ID(N'dbo.DonHang') AND name=N'CK_DonHang_7'
    AND is_disabled=0 AND is_not_trusted=0);
IF @definition IS NULL THROW 51202, 'Expected visible/trusted status constraint not found.', 1;
-- Strip formatting only OUTSIDE quoted literals. Never approve a different value
-- such as 'E X P I R E D', 'EX[PIRED]' or 'EXPIRED ' by normalizing its text.
DECLARE @shape nvarchar(max) = N'', @position int = 1, @quoted bit = 0,
    @character nchar(1);
WHILE @position <= LEN(@definition)
BEGIN
    SET @character = SUBSTRING(@definition,@position,1);
    IF @character=N'''' SET @quoted=CASE WHEN @quoted=0 THEN 1 ELSE 0 END;
    IF @quoted=1 OR @character=N''''
        SET @shape+=@character;
    ELSE IF @character NOT IN (N' ',N'[',N']',N'(',N')',NCHAR(9),NCHAR(13),NCHAR(10))
        SET @shape+=UPPER(@character);
    SET @position+=1;
END;
IF @quoted<>0 THROW 51205, 'Unbalanced status literal. Stop for review.', 1;
IF LEFT(@shape,11)=N'TRANGTHAIIN' SET @shape=SUBSTRING(@shape,12,LEN(@shape));
ELSE SET @shape=REPLACE(REPLACE(@shape,N'TRANGTHAI=',N''),N'OR',N',');
DECLARE @expected TABLE (status nvarchar(40) PRIMARY KEY);
INSERT INTO @expected(status) VALUES (N'DRAFT'),(N'PENDING_PAYMENT'),(N'PENDING_CONFIRMATION'),(N'ACCEPTED'),(N'PREPARING'),(N'READY'),(N'DELIVERING'),(N'COMPLETED'),(N'REJECTED'),(N'CANCELLED'),(N'DELIVERY_FAILED');
IF (SELECT COUNT(*) FROM STRING_SPLIT(@shape,N',')) <> (SELECT COUNT(*) FROM @expected)
    OR EXISTS (SELECT 1 FROM STRING_SPLIT(@shape,N',') s
        WHERE NOT EXISTS (SELECT 1 FROM @expected e WHERE s.value COLLATE Latin1_General_100_BIN2=(N''''+e.status+N'''') COLLATE Latin1_General_100_BIN2))
    OR EXISTS (SELECT 1 FROM @expected e
        WHERE NOT EXISTS (SELECT 1 FROM STRING_SPLIT(@shape,N',') s WHERE s.value COLLATE Latin1_General_100_BIN2=(N''''+e.status+N'''') COLLATE Latin1_General_100_BIN2))
    THROW 51205, 'Status CHECK differs from the reviewed baseline. Stop for review.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    ALTER TABLE dbo.DonHang DROP CONSTRAINT CK_DonHang_7;
    ALTER TABLE dbo.DonHang WITH CHECK ADD CONSTRAINT CK_DonHang_7 CHECK
        (TrangThai IN ('DRAFT','PENDING_PAYMENT','PENDING_CONFIRMATION','ACCEPTED','PREPARING','READY',
         'DELIVERING','COMPLETED','REJECTED','CANCELLED','DELIVERY_FAILED','EXPIRED'));
    ALTER TABLE dbo.DonHang CHECK CONSTRAINT CK_DonHang_7;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
-- No status backfill, data mutation, seed, FK/trigger changes or worker activation.
