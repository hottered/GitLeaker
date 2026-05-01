using GitLeaker.Enums;

namespace GitLeaker.Models;

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