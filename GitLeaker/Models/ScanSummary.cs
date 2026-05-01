namespace GitLeaker.Models;

public class ScanSummary
{
    public int TotalLeaks { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int CommitsScanned { get; set; }
    public int BranchesScanned { get; set; }
    public int DevelopersInvolved { get; set; }
    public string MostAffectedBranch { get; set; } = "";
    public string TopLeaker { get; set; } = "";
    public Dictionary<string, int> LeaksByType { get; set; } = new();
    public List<DeveloperStats> DeveloperLeaderboard { get; set; } = new();
    public List<BranchStats> BranchBreakdown { get; set; } = new();
    public List<LeakFinding> RecentFindings { get; set; } = new();
    public List<TimelinePoint> Timeline { get; set; } = new();
}