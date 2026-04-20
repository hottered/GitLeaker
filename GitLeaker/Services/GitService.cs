using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using GitLeaker.Models;

namespace GitLeaker.Services;

public record GitCommit(
    string Hash,
    string Author,
    string Email,
    DateTime Date,
    string Branch,
    string Message,
    List<(string FilePath, int LineNumber, string Content)> ChangedLines
);
 
public class GitService
{
    private readonly ILogger<GitService> _logger;
 
    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }
 
    // ─────────────────────────────────────────────
    //  REMOTE REPO SUPPORT
    // ─────────────────────────────────────────────
 
    /// <summary>
    /// Clones a remote repository into a temp directory and returns the path.
    /// The ScannerService is responsible for calling CleanupTempDir() when done.
    /// Supports GitHub, GitLab, Bitbucket, Azure DevOps, and any generic git URL.
    /// For private repos, pass an access token (GitHub PAT, GitLab PAT, Bitbucket app password).
    /// </summary>
    public async Task<(string path, string error)> CloneRemoteRepo(
        string repoUrl,
        string? accessToken,
        RepoProvider provider = RepoProvider.Auto)
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "leakradar_" + Guid.NewGuid().ToString("N")[..12]);
 
        Directory.CreateDirectory(tempPath);
 
        try
        {
            var cloneUrl = BuildAuthenticatedUrl(repoUrl, accessToken, provider);
            _logger.LogInformation("Cloning {Url} into {Path}", SanitizeUrlForLog(repoUrl), tempPath);
 
            // --no-single-branch fetches all remote branches so we can scan them all
            var args = $"clone --no-single-branch \"{cloneUrl}\" \"{tempPath}\"";
            var (_, stderr, exitCode) = await RunGitCommandRaw(null, args);
 
            if (exitCode != 0)
            {
                CleanupTempDir(tempPath);
                return ("", ParseGitError(stderr, repoUrl));
            }
 
            return (tempPath, "");
        }
        catch (Exception ex)
        {
            CleanupTempDir(tempPath);
            return ("", $"Clone error: {ex.Message}");
        }
    }
 
    /// <summary>
    /// Quick reachability check using git ls-remote — does not download history.
    /// Returns (true, "") if the repo is accessible, (false, errorMessage) otherwise.
    /// </summary>
    public async Task<(bool ok, string error)> ValidateRemoteUrl(
        string repoUrl,
        string? accessToken,
        RepoProvider provider = RepoProvider.Auto)
    {
        try
        {
            var authUrl = BuildAuthenticatedUrl(repoUrl, accessToken, provider);
            var (_, stderr, exitCode) = await RunGitCommandRaw(null, $"ls-remote --heads \"{authUrl}\"");
            return exitCode == 0
                ? (true, "")
                : (false, ParseGitError(stderr, repoUrl));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
 
    /// <summary>
    /// Safely deletes a temp directory created during a remote clone.
    /// </summary>
    public void CleanupTempDir(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                _logger.LogInformation("Cleaned up temp clone: {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not clean up temp dir {Path}: {Error}", path, ex.Message);
        }
    }
 
    // ─────────────────────────────────────────────
    //  URL HELPERS
    // ─────────────────────────────────────────────
 
    /// <summary>
    /// Builds an authenticated HTTPS clone URL for each supported provider.
    /// Handles SSH→HTTPS conversion automatically.
    /// </summary>
    private static string BuildAuthenticatedUrl(
        string repoUrl, string? token, RepoProvider provider)
    {
        if (string.IsNullOrWhiteSpace(token))
            return repoUrl;
 
        var url = repoUrl.Trim();
 
        // SSH URLs (git@github.com:user/repo.git) can't embed tokens — convert first
        if (url.StartsWith("git@"))
            url = ConvertSshToHttps(url);
 
        var uri = new Uri(url);
        provider = provider == RepoProvider.Auto ? DetectProvider(uri.Host) : provider;
 
        // Token embedding format differs per provider:
        //  GitHub:       https://TOKEN@github.com/...
        //  GitLab:       https://oauth2:TOKEN@gitlab.com/...
        //  Bitbucket:    https://x-token-auth:TOKEN@bitbucket.org/...
        //  Azure DevOps: https://anything:TOKEN@dev.azure.com/...
        //  Generic:      https://TOKEN@host/...
        var userInfo = provider switch
        {
            RepoProvider.GitLab => $"oauth2:{token}",
            RepoProvider.Bitbucket => $"x-token-auth:{token}",
            RepoProvider.AzureDevOps => $"anon:{token}",
            _ => token
        };
 
        return $"{uri.Scheme}://{userInfo}@{uri.Host}{uri.AbsolutePath}";
    }
 
    private static RepoProvider DetectProvider(string host) => host.ToLower() switch
    {
        var h when h.Contains("github") => RepoProvider.GitHub,
        var h when h.Contains("gitlab") => RepoProvider.GitLab,
        var h when h.Contains("bitbucket") => RepoProvider.Bitbucket,
        var h when h.Contains("dev.azure") || h.Contains("visualstudio") => RepoProvider.AzureDevOps,
        _ => RepoProvider.Generic
    };
 
    private static string ConvertSshToHttps(string sshUrl)
    {
        // git@github.com:user/repo.git  →  https://github.com/user/repo.git
        var match = Regex.Match(sshUrl, @"git@([^:]+):(.+)");
        return match.Success
            ? $"https://{match.Groups[1].Value}/{match.Groups[2].Value}"
            : sshUrl;
    }
 
    private static string SanitizeUrlForLog(string url)
    {
        try { var u = new Uri(url); return $"{u.Scheme}://{u.Host}{u.AbsolutePath}"; }
        catch { return "[url]"; }
    }
 
    private static string ParseGitError(string stderr, string repoUrl)
    {
        if (stderr.Contains("not found") || stderr.Contains("does not exist"))
            return "Repository not found. Check the URL and make sure it is correct.";
        if (stderr.Contains("Authentication failed") || stderr.Contains("could not read Username"))
            return "Authentication failed. Your access token may be missing, expired, or lack 'repo' scope.";
        if (stderr.Contains("unable to connect") || stderr.Contains("Could not resolve host"))
            return "Network error — could not reach the remote host.";
        return stderr.Trim()
                     .Split('\n')
                     .LastOrDefault(l => l.StartsWith("fatal:") || l.StartsWith("error:"))
                     ?.Trim()
               ?? "Unknown git error. Check your URL and token.";
    }
 
    // ─────────────────────────────────────────────
    //  LOCAL REPO OPERATIONS
    // ─────────────────────────────────────────────
 
    public async Task<bool> IsGitRepo(string path)
    {
        try
        {
            var (output, _, exitCode) = await RunGitCommandRaw(path, "rev-parse --git-dir");
            return exitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch { return false; }
    }
 
    public async Task<List<string>> GetBranches(string repoPath)
    {
        var (output, _, _) = await RunGitCommandRaw(repoPath, "branch -a --format=%(refname:short)");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Select(b => b.Trim())
                     .Where(b => !string.IsNullOrEmpty(b))
                     .ToList();
    }
 
    public async Task<string> GetCurrentBranch(string repoPath)
    {
        var (output, _, _) = await RunGitCommandRaw(repoPath, "rev-parse --abbrev-ref HEAD");
        return output.Trim();
    }
 
    public async Task<IAsyncEnumerable<GitCommit>> GetCommitsAsync(
        string repoPath,
        string? branch = null,
        int? daysBack = null,
        bool allBranches = false)
    {
        var args = new StringBuilder("log ");
 
        if (allBranches)
            args.Append("--all ");
        else if (!string.IsNullOrEmpty(branch))
            args.Append($"{branch} ");
 
        if (daysBack.HasValue)
            args.Append($"--since=\"{daysBack} days ago\" ");
 
        args.Append("--pretty=format:COMMIT_START|%H|%an|%ae|%aI|%s|COMMIT_END");
 
        var (logOutput, _, _) = await RunGitCommandRaw(repoPath, args.ToString());
        var currentBranch = branch ?? await GetCurrentBranch(repoPath);
 
        return ParseCommitsAsync(logOutput, repoPath, currentBranch);
    }
 
    private async IAsyncEnumerable<GitCommit> ParseCommitsAsync(
        string logOutput, string repoPath, string branch)
    {
        var lines = logOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
 
        foreach (var line in lines)
        {
            if (!line.StartsWith("COMMIT_START|")) continue;
 
            var parts = line.Replace("COMMIT_START|", "").Replace("|COMMIT_END", "").Split('|');
            if (parts.Length < 5) continue;
 
            string hash = parts[0];
            string author = parts[1];
            string email = parts[2];
            DateTime.TryParse(parts[3], out var date);
            string message = parts.Length > 4 ? parts[4] : "";
 
            var changedLines = await GetChangedLines(repoPath, hash);
            yield return new GitCommit(hash, author, email, date, branch, message, changedLines);
        }
    }
 
    private async Task<List<(string FilePath, int LineNumber, string Content)>> GetChangedLines(
        string repoPath, string commitHash)
    {
        var result = new List<(string, int, string)>();
        try
        {
            var (diff, _, _) = await RunGitCommandRaw(repoPath, $"diff-tree --no-commit-id -r -p {commitHash}");
            if (string.IsNullOrEmpty(diff)) return result;
 
            string currentFile = "";
            int lineNumber = 0;
 
            foreach (var line in diff.Split('\n'))
            {
                if (line.StartsWith("+++ b/"))
                {
                    currentFile = line[6..].Trim();
                    lineNumber = 0;
                }
                else if (line.StartsWith("@@"))
                {
                    var match = Regex.Match(line, @"\+(\d+)");
                    if (match.Success)
                        lineNumber = int.Parse(match.Groups[1].Value) - 1;
                }
                else if (line.StartsWith("+") && !line.StartsWith("+++"))
                {
                    lineNumber++;
                    string content = line[1..];
                    if (!string.IsNullOrWhiteSpace(content) && content.Length > 5)
                        result.Add((currentFile, lineNumber, content));
                }
                else if (!line.StartsWith("-"))
                {
                    lineNumber++;
                }
            }
        }
        catch { }
 
        return result;
    }
 
    // ─────────────────────────────────────────────
    //  GIT PROCESS RUNNER
    // ─────────────────────────────────────────────
 
    private static async Task<(string stdout, string stderr, int exitCode)> RunGitCommandRaw(
        string? workingDir, string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
 
        if (!string.IsNullOrEmpty(workingDir))
            psi.WorkingDirectory = workingDir;
 
        // Prevent git from hanging waiting for credentials in non-interactive environments
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["GIT_ASKPASS"] = "echo";
 
        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
 
        return (stdout, stderr, process.ExitCode);
    }
}