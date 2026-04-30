CREATE OR ALTER PROCEDURE usp_GetScanSummary
@ScanId NVARCHAR(36)
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Totals
    SELECT
        COUNT(*)                                            AS TotalLeaks,
        SUM(CASE WHEN Risk = 'Critical' THEN 1 ELSE 0 END) AS CriticalCount,
        SUM(CASE WHEN Risk = 'High'     THEN 1 ELSE 0 END) AS HighCount,
        SUM(CASE WHEN Risk = 'Medium'   THEN 1 ELSE 0 END) AS MediumCount,
        SUM(CASE WHEN Risk = 'Low'      THEN 1 ELSE 0 END) AS LowCount,
        COUNT(DISTINCT Branch)                              AS BranchesScanned,
        COUNT(DISTINCT AuthorEmail)                         AS DevelopersInvolved,
        s.CommitsScanned,
        s.FilesScanned
    FROM Findings f
             JOIN Scans s ON s.ScanId = f.ScanId
    WHERE f.ScanId = @ScanId
    GROUP BY s.CommitsScanned, s.FilesScanned;

    -- 2. Leaks by secret type
    SELECT SecretType, COUNT(*) AS Total
    FROM Findings
    WHERE ScanId = @ScanId
    GROUP BY SecretType
    ORDER BY Total DESC;

    -- 3. Developer leaderboard
    SELECT
        Author,
        AuthorEmail,
        COUNT(*)                                            AS TotalLeaks,
        SUM(CASE WHEN Risk = 'Critical' THEN 1 ELSE 0 END) AS CriticalLeaks,
        SUM(CASE WHEN Risk = 'High'     THEN 1 ELSE 0 END) AS HighLeaks,
        SUM(CASE WHEN Risk = 'Medium'   THEN 1 ELSE 0 END) AS MediumLeaks,
        SUM(CASE WHEN Risk = 'Low'      THEN 1 ELSE 0 END) AS LowLeaks,
        SUM(
                CASE Risk
                    WHEN 'Critical' THEN 10.0
                    WHEN 'High'     THEN 5.0
                    WHEN 'Medium'   THEN 2.0
                    ELSE 1.0
                    END
        )                                                   AS RiskScore,
        MAX(CommitDate)                                     AS LastLeakDate
    FROM Findings
    WHERE ScanId = @ScanId
    GROUP BY Author, AuthorEmail
    ORDER BY RiskScore DESC;

    -- 4. Branch breakdown
    SELECT
        Branch,
        COUNT(*)                                            AS TotalLeaks,
        SUM(CASE WHEN Risk = 'Critical' THEN 1 ELSE 0 END) AS CriticalLeaks,
        SUM(
                CASE Risk
                    WHEN 'Critical' THEN 10.0
                    WHEN 'High'     THEN 5.0
                    WHEN 'Medium'   THEN 2.0
                    ELSE 1.0
                    END
        )                                                   AS RiskScore
    FROM Findings
    WHERE ScanId = @ScanId
    GROUP BY Branch
    ORDER BY TotalLeaks DESC;

    -- 5. Timeline (grouped by day)
    SELECT
        CONVERT(NVARCHAR(10), CommitDate, 120)              AS Date,
        COUNT(*)                                            AS Count,
        SUM(CASE WHEN Risk = 'Critical' THEN 1 ELSE 0 END) AS Critical
    FROM Findings
    WHERE ScanId = @ScanId
    GROUP BY CONVERT(NVARCHAR(10), CommitDate, 120)
    ORDER BY Date;

    -- 6. Recent findings (top 20)
    SELECT TOP 20 *
    FROM Findings
    WHERE ScanId = @ScanId
    ORDER BY CommitDate DESC;
END