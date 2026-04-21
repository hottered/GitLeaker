namespace GitLeaker.Models;

public class LeakFinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CommitHash { get; set; } = "";
    public string CommitShort => CommitHash.Length >= 7 ? CommitHash[..7] : CommitHash;
    public string Author { get; set; } = "";
    public string AuthorEmail { get; set; } = "";
    public DateTime CommitDate { get; set; }
    public string Branch { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = "";
    public string RedactedContent { get; set; } = "";
    public string SecretType { get; set; } = "";
    public string MatchedPattern { get; set; } = "";
    public double Entropy { get; set; }
    public RiskLevel Risk { get; set; }
    public string CommitMessage { get; set; } = "";
    public string RemediationAdvice { get; set; } = "";
    public bool IsRevoked { get; set; } = false;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
}
 
public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
 
public class ScanRequest
{
    // LOCAL mode: path on disk
    public string? RepoPath { get; set; }
 
    // REMOTE mode: URL to clone
    public string? RepoUrl { get; set; }
 
    // Optional token for private repos (GitHub PAT, GitLab token, etc.)
    public string? AccessToken { get; set; }
 
    // Which provider to infer clone URL format (auto-detected if not set)
    public RepoProvider Provider { get; set; } = RepoProvider.Auto;
 
    public string? BranchFilter { get; set; }
    public int? DaysBack { get; set; }
    public bool ScanAllBranches { get; set; } = false;
    public double EntropyThreshold { get; set; } = 3.5;
 
    public bool IsRemote => !string.IsNullOrWhiteSpace(RepoUrl);
}
 
public enum RepoProvider
{
    Auto,
    GitHub,
    GitLab,
    Bitbucket,
    AzureDevOps,
    Generic
}
 
public class ScanResult
{
    public string ScanId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string RepoPath { get; set; } = "";
    public string? RepoUrl { get; set; }
    public bool IsRemote { get; set; } = false;
    public string? ClonedToPath { get; set; }
    public int CommitsScanned { get; set; }
    public int FilesScanned { get; set; }
    public List<LeakFinding> Findings { get; set; } = new();
    public ScanStatus Status { get; set; } = ScanStatus.Running;
    public string? Error { get; set; }
}
 
public enum ScanStatus
{
    Running,
    Completed,
    Failed
}
 
public class DeveloperStats
{
    public string Author { get; set; } = "";
    public string Email { get; set; } = "";
    public int TotalLeaks { get; set; }
    public int CriticalLeaks { get; set; }
    public int HighLeaks { get; set; }
    public int MediumLeaks { get; set; }
    public int LowLeaks { get; set; }
    public double RiskScore { get; set; }
    public string MostLeakedType { get; set; } = "";
    public DateTime? LastLeakDate { get; set; }
    public List<string> Branches { get; set; } = new();
}
 
public class BranchStats
{
    public string Branch { get; set; } = "";
    public int TotalLeaks { get; set; }
    public int CriticalLeaks { get; set; }
    public double RiskScore { get; set; }
    public List<string> TopLeakers { get; set; } = new();
}
 
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
 
public class TimelinePoint
{
    public string Date { get; set; } = "";
    public int Count { get; set; }
    public int Critical { get; set; }
}

public record GitCommit(
    string Hash,
    string Author,
    string Email,
    DateTime Date,
    string Branch,
    string Message,
    List<(string FilePath, int LineNumber, string Content)> ChangedLines
);

public record EntropyCheckRequest(string Input);

public record SecretPattern(
    string Name,
    string Regex,
    RiskLevel Risk,
    string Remediation,
    bool RequireEntropy = false,
    double MinEntropy = 3.0
);

