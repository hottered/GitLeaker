IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Scans')
BEGIN
CREATE TABLE Scans (
                       ScanId         NVARCHAR(36)  NOT NULL PRIMARY KEY,
                       RepoPath       NVARCHAR(500) NOT NULL,
                       RepoUrl        NVARCHAR(500) NULL,
                       IsRemote       BIT           NOT NULL DEFAULT 0,
                       Status         NVARCHAR(20)  NOT NULL DEFAULT 'Running',
                       CommitsScanned INT           NOT NULL DEFAULT 0,
                       FilesScanned   INT           NOT NULL DEFAULT 0,
                       StartedAt      DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                       CompletedAt    DATETIME2     NULL,
                       Error          NVARCHAR(MAX) NULL
);
CREATE INDEX IX_Scans_StartedAt ON Scans(StartedAt DESC);
END