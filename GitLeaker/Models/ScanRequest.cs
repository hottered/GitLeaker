using GitLeaker.Enums;

namespace GitLeaker.Models;

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