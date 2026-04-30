IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Findings')
BEGIN
CREATE TABLE Findings (
                          Id                INT IDENTITY(1,1) PRIMARY KEY,
                          ScanId            NVARCHAR(36)   NOT NULL
                          REFERENCES Scans(ScanId) ON DELETE CASCADE,
                          CommitHash        NVARCHAR(40)   NOT NULL,
                          Author            NVARCHAR(200)  NOT NULL,
                          AuthorEmail       NVARCHAR(200)  NOT NULL,
                          CommitDate        DATETIME2      NOT NULL,
                          Branch            NVARCHAR(200)  NOT NULL,
                          FilePath          NVARCHAR(1000) NOT NULL,
                          LineNumber        INT            NOT NULL,
                          LineContent       NVARCHAR(MAX)  NULL,
                          RedactedContent   NVARCHAR(MAX)  NULL,
                          SecretType        NVARCHAR(100)  NOT NULL,
                          MatchedPattern    NVARCHAR(200)  NULL,
                          Entropy           FLOAT          NOT NULL DEFAULT 0,
                          Risk              NVARCHAR(20)   NOT NULL,
                          CommitMessage     NVARCHAR(MAX)  NULL,
                          RemediationAdvice NVARCHAR(MAX)  NULL
);
CREATE INDEX IX_Findings_ScanId ON Findings(ScanId);
CREATE INDEX IX_Findings_Risk   ON Findings(Risk);
END