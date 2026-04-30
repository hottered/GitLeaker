CREATE OR ALTER PROCEDURE usp_InsertFinding
    @ScanId            NVARCHAR(36),
    @CommitHash        NVARCHAR(40),
    @Author            NVARCHAR(200),
    @AuthorEmail       NVARCHAR(200),
    @CommitDate        DATETIME2,
    @Branch            NVARCHAR(200),
    @FilePath          NVARCHAR(1000),
    @LineNumber        INT,
    @LineContent       NVARCHAR(MAX),
    @RedactedContent   NVARCHAR(MAX),
    @SecretType        NVARCHAR(100),
    @MatchedPattern    NVARCHAR(200),
    @Entropy           FLOAT,
    @Risk              NVARCHAR(20),
    @CommitMessage     NVARCHAR(MAX),
    @RemediationAdvice NVARCHAR(MAX)
    AS
BEGIN
    SET NOCOUNT ON;
INSERT INTO Findings (
    ScanId, CommitHash, Author, AuthorEmail, CommitDate, Branch,
    FilePath, LineNumber, LineContent, RedactedContent,
    SecretType, MatchedPattern, Entropy, Risk,
    CommitMessage, RemediationAdvice
)
VALUES (
           @ScanId, @CommitHash, @Author, @AuthorEmail, @CommitDate, @Branch,
           @FilePath, @LineNumber, @LineContent, @RedactedContent,
           @SecretType, @MatchedPattern, @Entropy, @Risk,
           @CommitMessage, @RemediationAdvice
       );
END