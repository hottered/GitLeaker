using GitLeaker.Models;
using GitLeaker.Services.Interfaces;

namespace GitLeaker.Services;

public class ReportService : IReportService
{
    public ScanSummary GenerateSummary(ScanResult scan)
    {
        var findings = scan.Findings;
 
        var devStats = findings
            .GroupBy(f => f.AuthorEmail)
            .Select(g =>
            {
                var leaks = g.ToList();
                return new DeveloperStats
                {
                    Author = leaks.First().Author,
                    Email = g.Key,
                    TotalLeaks = leaks.Count,
                    CriticalLeaks = leaks.Count(l => l.Risk == RiskLevel.Critical),
                    HighLeaks = leaks.Count(l => l.Risk == RiskLevel.High),
                    MediumLeaks = leaks.Count(l => l.Risk == RiskLevel.Medium),
                    LowLeaks = leaks.Count(l => l.Risk == RiskLevel.Low),
                    RiskScore = CalculateDevRiskScore(leaks),
                    MostLeakedType = leaks.GroupBy(l => l.SecretType)
                        .OrderByDescending(g => g.Count()).First().Key,
                    LastLeakDate = leaks.Max(l => l.CommitDate),
                    Branches = leaks.Select(l => l.Branch).Distinct().ToList()
                };
            })
            .OrderByDescending(d => d.RiskScore)
            .ToList();
 
        var branchStats = findings
            .GroupBy(f => f.Branch)
            .Select(g =>
            {
                var leaks = g.ToList();
                return new BranchStats
                {
                    Branch = g.Key,
                    TotalLeaks = leaks.Count,
                    CriticalLeaks = leaks.Count(l => l.Risk == RiskLevel.Critical),
                    RiskScore = CalculateDevRiskScore(leaks),
                    TopLeakers = leaks.GroupBy(l => l.Author)
                        .OrderByDescending(a => a.Count())
                        .Take(3)
                        .Select(a => a.Key)
                        .ToList()
                };
            })
            .OrderByDescending(b => b.TotalLeaks)
            .ToList();
 
        // Generate timeline (group by day)
        var timeline = findings
            .GroupBy(f => f.CommitDate.Date.ToString("yyyy-MM-dd"))
            .OrderBy(g => g.Key)
            .Select(g => new TimelinePoint
            {
                Date = g.Key,
                Count = g.Count(),
                Critical = g.Count(l => l.Risk == RiskLevel.Critical)
            })
            .ToList();
 
        return new ScanSummary
        {
            TotalLeaks = findings.Count,
            CriticalCount = findings.Count(f => f.Risk == RiskLevel.Critical),
            HighCount = findings.Count(f => f.Risk == RiskLevel.High),
            MediumCount = findings.Count(f => f.Risk == RiskLevel.Medium),
            LowCount = findings.Count(f => f.Risk == RiskLevel.Low),
            CommitsScanned = scan.CommitsScanned,
            BranchesScanned = findings.Select(f => f.Branch).Distinct().Count(),
            DevelopersInvolved = devStats.Count,
            MostAffectedBranch = branchStats.FirstOrDefault()?.Branch ?? "—",
            TopLeaker = devStats.FirstOrDefault()?.Author ?? "—",
            LeaksByType = findings.GroupBy(f => f.SecretType)
                .ToDictionary(g => g.Key, g => g.Count()),
            DeveloperLeaderboard = devStats,
            BranchBreakdown = branchStats,
            RecentFindings = findings.OrderByDescending(f => f.CommitDate).Take(20).ToList(),
            Timeline = timeline
        };
    }
 
    private static double CalculateDevRiskScore(List<LeakFinding> leaks)
    {
        // Weighted score: Critical=10, High=5, Medium=2, Low=1
        return leaks.Sum(l => l.Risk switch
        {
            RiskLevel.Critical => 10.0,
            RiskLevel.High => 5.0,
            RiskLevel.Medium => 2.0,
            _ => 1.0
        });
    }
}