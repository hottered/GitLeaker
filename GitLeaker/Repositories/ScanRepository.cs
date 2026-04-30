using System.Data;
using Dapper;
using GitLeaker.Data;
using GitLeaker.Models;
using GitLeaker.Repositories.Interfaces;

namespace GitLeaker.Repositories;

public class ScanRepository : IScanRepository
{
    private readonly IDbConnectionFactory _factory;

    public ScanRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task CreateScanAsync(ScanResult scan)
    {
        using var db = _factory.Create();
        await db.ExecuteAsync("usp_InsertScan",
            new
            {
                scan.ScanId,
                scan.RepoPath,
                scan.RepoUrl,
                scan.IsRemote,
                Status = scan.Status.ToString()
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateScanAsync(ScanResult scan)
    {
        using var db = _factory.Create();
        await db.ExecuteAsync("usp_UpdateScan",
            new
            {
                scan.ScanId,
                Status         = scan.Status.ToString(),
                scan.CommitsScanned,
                scan.FilesScanned,
                scan.CompletedAt,
                scan.Error
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task AddFindingAsync(string scanId, LeakFinding f)
    {
        using var db = _factory.Create();
        await db.ExecuteAsync("usp_InsertFinding",
            new
            {
                ScanId             = scanId,
                f.CommitHash,
                f.Author,
                f.AuthorEmail,
                f.CommitDate,
                f.Branch,
                f.FilePath,
                f.LineNumber,
                f.LineContent,
                f.RedactedContent,
                f.SecretType,
                f.MatchedPattern,
                f.Entropy,
                Risk               = f.Risk.ToString(),
                f.CommitMessage,
                f.RemediationAdvice
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ScanResult?> GetScanAsync(string scanId)
    {
        using var db = _factory.Create();

        using var multi = await db.QueryMultipleAsync("usp_GetScan",
            new { ScanId = scanId },
            commandType: CommandType.StoredProcedure);

        var scan = await multi.ReadFirstOrDefaultAsync<ScanRow>();
        if (scan is null) return null;

        var findings = (await multi.ReadAsync<FindingRow>()).ToList();

        return MapToScanResult(scan, findings);
    }

    public async Task<List<ScanResult>> GetAllScansAsync()
    {
        using var db = _factory.Create();

        var rows = await db.QueryAsync<ScanRow>("usp_GetAllScans",
            commandType: CommandType.StoredProcedure);

        return rows.Select(r => MapToScanResult(r, [])).ToList();
    }

    public async Task<ScanSummary> GetScanSummaryAsync(string scanId)
    {
        using var db = _factory.Create();

        using var multi = await db.QueryMultipleAsync("usp_GetScanSummary",
            new { ScanId = scanId },
            commandType: CommandType.StoredProcedure);

        var totals       = await multi.ReadFirstOrDefaultAsync<TotalsRow>();
        var byType       = (await multi.ReadAsync<(string SecretType, int Total)>()).ToList();
        var devRows      = (await multi.ReadAsync<DevRow>()).ToList();
        var branchRows   = (await multi.ReadAsync<BranchRow>()).ToList();
        var timelineRows = (await multi.ReadAsync<TimelineRow>()).ToList();
        var recent       = (await multi.ReadAsync<FindingRow>()).ToList();

        return new ScanSummary
        {
            TotalLeaks          = totals?.TotalLeaks ?? 0,
            CriticalCount       = totals?.CriticalCount ?? 0,
            HighCount           = totals?.HighCount ?? 0,
            MediumCount         = totals?.MediumCount ?? 0,
            LowCount            = totals?.LowCount ?? 0,
            CommitsScanned      = totals?.CommitsScanned ?? 0,
            BranchesScanned     = totals?.BranchesScanned ?? 0,
            DevelopersInvolved  = totals?.DevelopersInvolved ?? 0,
            LeaksByType         = byType.ToDictionary(r => r.SecretType, r => r.Total),
            MostAffectedBranch  = branchRows.FirstOrDefault()?.Branch ?? "—",
            TopLeaker           = devRows.FirstOrDefault()?.Author ?? "—",
            DeveloperLeaderboard = devRows.Select(d => new DeveloperStats
            {
                Author        = d.Author,
                Email         = d.AuthorEmail,
                TotalLeaks    = d.TotalLeaks,
                CriticalLeaks = d.CriticalLeaks,
                HighLeaks     = d.HighLeaks,
                MediumLeaks   = d.MediumLeaks,
                LowLeaks      = d.LowLeaks,
                RiskScore     = (double)d.RiskScore,
                LastLeakDate  = d.LastLeakDate
            }).ToList(),
            BranchBreakdown = branchRows.Select(b => new BranchStats
            {
                Branch       = b.Branch,
                TotalLeaks   = b.TotalLeaks,
                CriticalLeaks = b.CriticalLeaks,
                RiskScore    = (double)b.RiskScore
            }).ToList(),
            Timeline = timelineRows.Select(t => new TimelinePoint
            {
                Date     = t.Date,
                Count    = t.Count,
                Critical = t.Critical
            }).ToList(),
            RecentFindings = recent.Select(MapToLeakFinding).ToList()
        };
    }

    // ── MAPPING ───────────────────────────────────────────────────────

    private static ScanResult MapToScanResult(ScanRow r, IEnumerable<FindingRow> findings)
        => new()
        {
            ScanId         = r.ScanId,
            RepoPath       = r.RepoPath,
            RepoUrl        = r.RepoUrl,
            IsRemote       = r.IsRemote,
            Status         = Enum.Parse<ScanStatus>(r.Status),
            CommitsScanned = r.CommitsScanned,
            FilesScanned   = r.FilesScanned,
            StartedAt      = r.StartedAt,
            CompletedAt    = r.CompletedAt,
            Error          = r.Error,
            Findings       = findings.Select(MapToLeakFinding).ToList()
        };

    private static LeakFinding MapToLeakFinding(FindingRow r)
        => new()
        {
            CommitHash        = r.CommitHash,
            Author            = r.Author,
            AuthorEmail       = r.AuthorEmail,
            CommitDate        = r.CommitDate,
            Branch            = r.Branch,
            FilePath          = r.FilePath,
            LineNumber        = r.LineNumber,
            LineContent       = r.LineContent,
            RedactedContent   = r.RedactedContent,
            SecretType        = r.SecretType,
            MatchedPattern    = r.MatchedPattern,
            Entropy           = r.Entropy,
            Risk              = Enum.Parse<RiskLevel>(r.Risk),
            CommitMessage     = r.CommitMessage,
            RemediationAdvice = r.RemediationAdvice
        };

    // ── PRIVATE DB ROWS ───────────────────────────────────────────────

    private record ScanRow(
        string ScanId, string RepoPath, string? RepoUrl, bool IsRemote,
        string Status, int CommitsScanned, int FilesScanned,
        DateTime StartedAt, DateTime? CompletedAt, string? Error);

    private record FindingRow(
        int Id, string ScanId,
        string CommitHash, string Author, string AuthorEmail,
        DateTime CommitDate, string Branch, string FilePath,
        int LineNumber, string? LineContent, string? RedactedContent,
        string SecretType, string? MatchedPattern, double Entropy, string Risk,
        string? CommitMessage, string? RemediationAdvice);

    private record TotalsRow(
        int TotalLeaks, int CriticalCount, int HighCount,
        int MediumCount, int LowCount, int BranchesScanned,
        int DevelopersInvolved, int CommitsScanned, int FilesScanned);

    private record DevRow(
        string Author, string AuthorEmail,
        int TotalLeaks, int CriticalLeaks, int HighLeaks,
        int MediumLeaks, int LowLeaks, decimal RiskScore, DateTime LastLeakDate);

    private record BranchRow(
        string Branch, int TotalLeaks, int CriticalLeaks, decimal RiskScore);

    private record TimelineRow(string Date, int Count, int Critical);
}