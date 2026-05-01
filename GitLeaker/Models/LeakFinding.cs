using GitLeaker.Enums;

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