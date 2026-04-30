CREATE OR ALTER PROCEDURE usp_InsertScan
    @ScanId         NVARCHAR(36),
    @RepoPath       NVARCHAR(500),
    @RepoUrl        NVARCHAR(500),
    @IsRemote       BIT,
    @Status         NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Scans (ScanId, RepoPath, RepoUrl, IsRemote, Status)
    VALUES (@ScanId, @RepoPath, @RepoUrl, @IsRemote, @Status);
END