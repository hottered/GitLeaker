using GitLeaker.Models;
using GitLeaker.Services.Interfaces;

namespace GitLeaker.Services;

public class ScannerService : IScannerService
{
    private readonly IEntropyService _entropy;
    private readonly IPatternService _patterns;
    private readonly IGitService _git;
    private readonly ITokenService _token;

    // In-memory store for MVP (replace with DB in production)
    private static readonly Dictionary<string, ScanResult> _scans = new();

    public ScannerService(
        IEntropyService entropy,
        IPatternService patterns,
        IGitService git,
        ITokenService token)
    {
        _entropy = entropy;
        _patterns = patterns;
        _git = git;
        _token = token;
    }

    public async Task<string> StartScanAsync(ScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepoPath) && string.IsNullOrWhiteSpace(request.RepoUrl))
            throw new ArgumentException("Either RepoPath (local) or RepoUrl (remote) must be provided.");

        if (string.IsNullOrWhiteSpace(request.AccessToken))
            request.AccessToken = await _token.GetGitHubTokenAsync();

        var displayName = request.IsRemote ? request.RepoUrl! : request.RepoPath!;

        var scan = new ScanResult
        {
            RepoPath = displayName,
            RepoUrl = request.RepoUrl,
            IsRemote = request.IsRemote,
            Status = ScanStatus.Running
        };

        _scans[scan.ScanId] = scan;

        // Fire and forget — client polls /status
        _ = Task.Run(async () => await ExecuteScanAsync(scan, request));

        return scan.ScanId;
    }

    private async Task ExecuteScanAsync(ScanResult scan, ScanRequest request)
    {
        try
        {
            IAsyncEnumerable<GitCommit> commits;

            if (request.IsRemote)
            {
                // ── REMOTE: use GitHub API directly, no cloning ────────
                commits = await _git.GetCommitsFromApiAsync(
                    request.RepoUrl!,
                    request.AccessToken!,
                    request.BranchFilter,
                    request.DaysBack,
                    request.ScanAllBranches);
            }
            else
            {
                // ── LOCAL: validate path, use git CLI ──────────────────
                if (!await _git.IsGitRepo(request.RepoPath!))
                {
                    scan.Status = ScanStatus.Failed;
                    scan.Error = "Not a valid git repository. Check the path and make sure git is initialised.";
                    return;
                }

                commits = await _git.GetCommitsAsync(
                    request.RepoPath!,
                    request.BranchFilter,
                    request.DaysBack,
                    request.ScanAllBranches);
            }

            // ── SCAN ────────────────────────────────────────────────────
            var seenKeys = new HashSet<string>();

            await foreach (var commit in commits)
            {
                scan.CommitsScanned++;
                var processedFiles = new HashSet<string>();

                foreach (var (filePath, lineNumber, content) in commit.ChangedLines)
                {
                    if (ShouldSkipFile(filePath)) continue;
                    processedFiles.Add(filePath);

                    var findings = AnalyzeLine(content, commit, filePath, lineNumber);

                    foreach (var finding in findings)
                    {
                        var key = $"{filePath}:{lineNumber}:{finding.SecretType}";
                        if (seenKeys.Contains(key)) continue;
                        seenKeys.Add(key);
                        scan.Findings.Add(finding);
                    }
                }

                scan.FilesScanned += processedFiles.Count;
            }

            scan.Status = ScanStatus.Completed;
            scan.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            scan.Status = ScanStatus.Failed;
            scan.Error = ex.Message;
        }
        // No finally/cleanup needed — remote scans never touch the filesystem
    }

    private List<LeakFinding> AnalyzeLine(
        string line, GitCommit commit, string filePath, int lineNumber)
    {
        var findings = new List<LeakFinding>();

        // 1. Pattern-based detection
        var patternMatches = _patterns.Scan(line);
        foreach (var (pattern, match) in patternMatches)
        {
            var secretValue = match.Groups.Count > 1 ? match.Groups[^1].Value : match.Value;
            var entropy = _entropy.Calculate(secretValue);

            if (pattern.RequireEntropy && entropy < pattern.MinEntropy)
                continue;

            findings.Add(new LeakFinding
            {
                CommitHash = commit.Hash,
                Author = commit.Author,
                AuthorEmail = commit.Email,
                CommitDate = commit.Date,
                Branch = commit.Branch,
                FilePath = filePath,
                LineNumber = lineNumber,
                LineContent = line.Trim(),
                RedactedContent = _entropy.RedactSecret(line.Trim(), secretValue),
                SecretType = pattern.Name,
                MatchedPattern = pattern.Regex[..Math.Min(40, pattern.Regex.Length)] + "...",
                Entropy = entropy,
                Risk = pattern.Risk,
                CommitMessage = commit.Message,
                RemediationAdvice = pattern.Remediation
            });
        }

        // 2. Entropy-only fallback (catches unknown secret formats)
        if (!findings.Any())
        {
            var (token, entropy) = _entropy.ExtractHighEntropyToken(line, 4.0);
            if (!string.IsNullOrEmpty(token))
            {
                findings.Add(new LeakFinding
                {
                    CommitHash = commit.Hash,
                    Author = commit.Author,
                    AuthorEmail = commit.Email,
                    CommitDate = commit.Date,
                    Branch = commit.Branch,
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    LineContent = line.Trim(),
                    RedactedContent = _entropy.RedactSecret(line.Trim(), token),
                    SecretType = "High Entropy String",
                    MatchedPattern = "entropy-based",
                    Entropy = entropy,
                    Risk = _patterns.GetRiskFromEntropy(entropy),
                    CommitMessage = commit.Message,
                    RemediationAdvice = "High entropy string detected. Verify if this is a secret and move it to a secure secrets manager."
                });
            }
        }

        return findings;
    }

    private static bool ShouldSkipFile(string filePath)
    {
        var skipExtensions = new[] {
            ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg",
            ".woff", ".woff2", ".ttf", ".eot", ".pdf",
            ".zip", ".tar", ".gz", ".lock", ".min.js", ".min.css"
        };
        var skipPaths = new[] {
            "node_modules/", ".git/", "vendor/", "dist/", "build/", "__pycache__/"
        };

        var lower = filePath.ToLowerInvariant();
        return skipExtensions.Any(ext => lower.EndsWith(ext)) ||
               skipPaths.Any(path => lower.Contains(path));
    }

    public ScanResult? GetScan(string scanId) =>
        _scans.GetValueOrDefault(scanId);

    public List<ScanResult> GetAllScans() =>
        _scans.Values.OrderByDescending(s => s.StartedAt).ToList();
}