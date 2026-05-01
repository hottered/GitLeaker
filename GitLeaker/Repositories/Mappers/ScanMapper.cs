using GitLeaker.Enums;
using GitLeaker.Models;
using GitLeaker.Repositories.DataRows;

namespace GitLeaker.Repositories.Mappers;

internal sealed class ScanMapper
{
    public ScanResult ToScanResult(ScanRow row, IEnumerable<FindingRow> findings) => new()
    {
        ScanId         = row.ScanId,
        RepoPath       = row.RepoPath,
        RepoUrl        = row.RepoUrl,
        IsRemote       = row.IsRemote,
        Status         = Enum.Parse<ScanStatus>(row.Status),
        CommitsScanned = row.CommitsScanned,
        FilesScanned   = row.FilesScanned,
        StartedAt      = row.StartedAt,
        CompletedAt    = row.CompletedAt,
        Error          = row.Error,
        Findings       = findings.Select(ToLeakFinding).ToList()
    };

    public LeakFinding ToLeakFinding(FindingRow row) => new()
    {
        CommitHash        = row.CommitHash,
        Author            = row.Author,
        AuthorEmail       = row.AuthorEmail,
        CommitDate        = row.CommitDate,
        Branch            = row.Branch,
        FilePath          = row.FilePath,
        LineNumber        = row.LineNumber,
        LineContent       = row.LineContent ?? "",
        RedactedContent   = row.RedactedContent ?? "",
        SecretType        = row.SecretType,
        MatchedPattern    = row.MatchedPattern ?? "",
        Entropy           = row.Entropy,
        Risk              = Enum.Parse<RiskLevel>(row.Risk),
        CommitMessage     = row.CommitMessage ?? "",
        RemediationAdvice = row.RemediationAdvice ?? ""
    };

    public ScanSummary ToScanSummary(
        TotalsRow?                             totals,
        IList<(string SecretType, int Total)>  byType,
        IList<DevRow>                          devRows,
        IList<BranchRow>                       branchRows,
        IList<TimelineRow>                     timelineRows,
        IList<FindingRow>                      recent) => new()
    {
        TotalLeaks           = totals?.TotalLeaks ?? 0,
        CriticalCount        = totals?.CriticalCount ?? 0,
        HighCount            = totals?.HighCount ?? 0,
        MediumCount          = totals?.MediumCount ?? 0,
        LowCount             = totals?.LowCount ?? 0,
        CommitsScanned       = totals?.CommitsScanned ?? 0,
        BranchesScanned      = totals?.BranchesScanned ?? 0,
        DevelopersInvolved   = totals?.DevelopersInvolved ?? 0,
        LeaksByType          = byType.ToDictionary(r => r.SecretType, r => r.Total),
        MostAffectedBranch   = branchRows.FirstOrDefault()?.Branch ?? "—",
        TopLeaker            = devRows.FirstOrDefault()?.Author ?? "—",
        DeveloperLeaderboard = devRows.Select(ToDeveloperStats).ToList(),
        BranchBreakdown      = branchRows.Select(ToBranchStats).ToList(),
        Timeline             = timelineRows.Select(ToTimelinePoint).ToList(),
        RecentFindings       = recent.Select(ToLeakFinding).ToList()
    };

    private static DeveloperStats ToDeveloperStats(DevRow row) => new()
    {
        Author        = row.Author,
        Email         = row.AuthorEmail,
        TotalLeaks    = row.TotalLeaks,
        CriticalLeaks = row.CriticalLeaks,
        HighLeaks     = row.HighLeaks,
        MediumLeaks   = row.MediumLeaks,
        LowLeaks      = row.LowLeaks,
        RiskScore     = (double)row.RiskScore,
        LastLeakDate  = row.LastLeakDate
    };

    private static BranchStats ToBranchStats(BranchRow row) => new()
    {
        Branch        = row.Branch,
        TotalLeaks    = row.TotalLeaks,
        CriticalLeaks = row.CriticalLeaks,
        RiskScore     = (double)row.RiskScore
    };

    private static TimelinePoint ToTimelinePoint(TimelineRow row) => new()
    {
        Date     = row.Date,
        Count    = row.Count,
        Critical = row.Critical
    };
    
}