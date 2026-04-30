using GitLeaker.Models;
using GitLeaker.Repositories.Interfaces;
using GitLeaker.Services.Interfaces;

namespace GitLeaker.Services;

public class ScannerService : IScannerService
{
    private readonly IEntropyService _entropy;
    private readonly IPatternService _patterns;
    private readonly IGitService _git;
    private readonly ITokenService _token;
    private readonly IScanRepository _repo;

    public ScannerService(
        IEntropyService entropy,
        IPatternService patterns,
        IGitService git,
        ITokenService token,
        IScanRepository repo)
    {
        _entropy  = entropy;
        _patterns = patterns;
        _git      = git;
        _token    = token;
        _repo     = repo;
    }

    public async Task<string> StartScanAsync(ScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepoPath) && string.IsNullOrWhiteSpace(request.RepoUrl))
            throw new ArgumentException("Either RepoPath or RepoUrl must be provided.");

        if (string.IsNullOrWhiteSpace(request.AccessToken))
            request.AccessToken = await _token.GetGitHubTokenAsync();

        var scan = new ScanResult
        {
            RepoPath = request.IsRemote ? request.RepoUrl! : request.RepoPath!,
            RepoUrl  = request.RepoUrl,
            IsRemote = request.IsRemote,
            Status   = ScanStatus.Running
        };

        await _repo.CreateScanAsync(scan);

        // Fire-and-forget — client polls for status
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
                commits = await _git.GetCommitsFromApiAsync(
                    request.RepoUrl!, request.AccessToken!,
                    request.BranchFilter, request.DaysBack, request.ScanAllBranches);
            }
            else
            {
                if (!await _git.IsGitRepo(request.RepoPath!))
                {
                    scan.Status = ScanStatus.Failed;
                    scan.Error  = "Not a valid git repository.";
                    await _repo.UpdateScanAsync(scan);
                    return;
                }

                commits = await _git.GetCommitsAsync(
                    request.RepoPath!, request.BranchFilter,
                    request.DaysBack, request.ScanAllBranches);
            }

            var seenKeys = new HashSet<string>();

            await foreach (var commit in commits)
            {
                scan.CommitsScanned++;
                var processedFiles = new HashSet<string>();

                foreach (var (filePath, lineNumber, content) in commit.ChangedLines)
                {
                    if (ShouldSkipFile(filePath)) continue;
                    processedFiles.Add(filePath);

                    foreach (var finding in AnalyzeLine(content, commit, filePath, lineNumber))
                    {
                        var key = $"{filePath}:{lineNumber}:{finding.SecretType}";
                        if (!seenKeys.Add(key)) continue;

                        // Written straight to DB — no in-memory list
                        await _repo.AddFindingAsync(scan.ScanId, finding);
                    }
                }

                scan.FilesScanned += processedFiles.Count;
            }

            scan.Status      = ScanStatus.Completed;
            scan.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            scan.Status = ScanStatus.Failed;
            scan.Error  = ex.Message;
        }

        await _repo.UpdateScanAsync(scan);
    }

    public async Task<ScanResult?> GetScanAsync(string scanId)
        => await _repo.GetScanAsync(scanId);

    public async Task<List<ScanResult>> GetAllScansAsync()
        => await _repo.GetAllScansAsync();

    // ── private helpers — completely unchanged ────────────────────────

    private List<LeakFinding> AnalyzeLine(
        string line, GitCommit commit, string filePath, int lineNumber)
    {
        var findings = new List<LeakFinding>();

        var patternMatches = _patterns.Scan(line);
        foreach (var (pattern, match) in patternMatches)
        {
            var secretValue = match.Groups.Count > 1 ? match.Groups[^1].Value : match.Value;
            var entropy     = _entropy.Calculate(secretValue);

            if (pattern.RequireEntropy && entropy < pattern.MinEntropy) continue;

            findings.Add(new LeakFinding
            {
                CommitHash        = commit.Hash,
                Author            = commit.Author,
                AuthorEmail       = commit.Email,
                CommitDate        = commit.Date,
                Branch            = commit.Branch,
                FilePath          = filePath,
                LineNumber        = lineNumber,
                LineContent       = line.Trim(),
                RedactedContent   = _entropy.RedactSecret(line.Trim(), secretValue),
                SecretType        = pattern.Name,
                MatchedPattern    = pattern.Regex[..Math.Min(40, pattern.Regex.Length)] + "...",
                Entropy           = entropy,
                Risk              = pattern.Risk,
                CommitMessage     = commit.Message,
                RemediationAdvice = pattern.Remediation
            });
        }

        if (!findings.Any())
        {
            var (token, entropy) = _entropy.ExtractHighEntropyToken(line, 4.0);
            if (!string.IsNullOrEmpty(token))
            {
                findings.Add(new LeakFinding
                {
                    CommitHash        = commit.Hash,
                    Author            = commit.Author,
                    AuthorEmail       = commit.Email,
                    CommitDate        = commit.Date,
                    Branch            = commit.Branch,
                    FilePath          = filePath,
                    LineNumber        = lineNumber,
                    LineContent       = line.Trim(),
                    RedactedContent   = _entropy.RedactSecret(line.Trim(), token),
                    SecretType        = "High Entropy String",
                    MatchedPattern    = "entropy-based",
                    Entropy           = entropy,
                    Risk              = _patterns.GetRiskFromEntropy(entropy),
                    CommitMessage     = commit.Message,
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
}