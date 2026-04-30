CREATE OR ALTER PROCEDURE usp_UpdateScan
    @ScanId         NVARCHAR(36),
    @Status         NVARCHAR(20),
    @CommitsScanned INT,
    @FilesScanned   INT,
    @CompletedAt    DATETIME2,
    @Error          NVARCHAR(MAX)
    AS
BEGIN
    SET NOCOUNT ON;
UPDATE Scans SET
                 Status         = @Status,
                 CommitsScanned = @CommitsScanned,
                 FilesScanned   = @FilesScanned,
                 CompletedAt    = @CompletedAt,
                 Error          = @Error
WHERE ScanId = @ScanId;
END