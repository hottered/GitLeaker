using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GitLeaker.Models;
using GitLeaker.Services.Interfaces;

namespace GitLeaker.Services;

public class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public GitService(ILogger<GitService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    // ─────────────────────────────────────────────
    //  REMOTE: GITHUB API (no cloning)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Streams commits + their diffs directly from the GitHub API.
    /// No temp directories, no git binary, no cleanup needed.
    /// </summary>
    public async Task<IAsyncEnumerable<GitCommit>> GetCommitsFromApiAsync(
        string repoUrl,
        string accessToken,
        string? branch = null,
        int? daysBack = null,
        bool allBranches = false)
    {
        var (owner, repo) = ParseOwnerRepo(repoUrl);
        return FetchCommitsFromApiAsync(owner, repo, accessToken, branch, daysBack, allBranches);
    }

    private async IAsyncEnumerable<GitCommit> FetchCommitsFromApiAsync(
        string owner,
        string repo,
        string accessToken,
        string? branch,
        int? daysBack,
        bool allBranches)
    {
        var branches = allBranches
            ? await GetApiBranches(owner, repo, accessToken)
            : new List<string> { branch ?? await GetDefaultBranch(owner, repo, accessToken) };

        var seenCommits = new HashSet<string>();

        foreach (var branchName in branches)
        {
            var page = 1;

            while (true)
            {
                var queryParams = new List<string>
                {
                    $"sha={Uri.EscapeDataString(branchName)}",
                    $"per_page=100",
                    $"page={page}"
                };

                if (daysBack.HasValue)
                {
                    var since = DateTime.UtcNow.AddDays(-daysBack.Value).ToString("o");
                    queryParams.Add($"since={Uri.EscapeDataString(since)}");
                }

                var listUrl = $"https://api.github.com/repos/{owner}/{repo}/commits?{string.Join("&", queryParams)}";
                var commitList = await ApiGetJson<JsonElement>(listUrl, accessToken);

                if (commitList.ValueKind != JsonValueKind.Array) break;

                var commits = commitList.EnumerateArray().ToList();
                if (commits.Count == 0) break;

                foreach (var c in commits)
                {
                    var sha = c.GetProperty("sha").GetString()!;
                    if (!seenCommits.Add(sha)) continue; // deduplicate across branches

                    var commit = await FetchSingleCommitAsync(owner, repo, sha, branchName, accessToken);
                    if (commit != null)
                        yield return commit;
                }

                if (commits.Count < 100) break; // last page
                page++;
            }
        }
    }

    private async Task<GitCommit?> FetchSingleCommitAsync(
        string owner, string repo, string sha, string branch, string accessToken)
    {
        // Request raw diff format directly — same format as git diff-tree output
        var client = CreateClient(accessToken);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3.diff"));

        var diffUrl = $"https://api.github.com/repos/{owner}/{repo}/commits/{sha}";

        // First fetch metadata (author, date, message) as JSON
        var metaUrl = $"https://api.github.com/repos/{owner}/{repo}/commits/{sha}";
        var meta = await ApiGetJson<JsonElement>(metaUrl, accessToken);

        string author = "";
        string email = "";
        DateTime date = DateTime.UtcNow;
        string message = "";

        if (meta.ValueKind == JsonValueKind.Object)
        {
            var commitObj = meta.GetProperty("commit");
            author = commitObj.GetProperty("author").GetProperty("name").GetString() ?? "";
            email = commitObj.GetProperty("author").GetProperty("email").GetString() ?? "";
            message = commitObj.GetProperty("message").GetString() ?? "";
            DateTime.TryParse(
                commitObj.GetProperty("author").GetProperty("date").GetString(),
                out date);
        }

        // Now fetch the raw diff
        var diffResponse = await client.GetAsync(diffUrl);
        var rawDiff = await diffResponse.Content.ReadAsStringAsync();

        var changedLines = ParseDiff(rawDiff);

        return new GitCommit(sha, author, email, date, branch, message, changedLines);
    }

    /// <summary>
    /// Parses a raw unified diff (same format from API or git diff-tree).
    /// Only extracts added lines (+) since those are what we scan for leaks.
    /// </summary>
    private static List<(string FilePath, int LineNumber, string Content)> ParseDiff(string diff)
    {
        var result = new List<(string, int, string)>();
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
                var content = line[1..];
                if (!string.IsNullOrWhiteSpace(content) && content.Length > 5)
                    result.Add((currentFile, lineNumber, content));
            }
            else if (!line.StartsWith("-"))
            {
                lineNumber++;
            }
        }

        return result;
    }

    // ─────────────────────────────────────────────
    //  REMOTE: VALIDATION
    // ─────────────────────────────────────────────

    public async Task<(bool ok, string error)> ValidateRemoteUrl(
        string repoUrl,
        string? accessToken,
        RepoProvider provider = RepoProvider.Auto)
    {
        try
        {
            var (owner, repo) = ParseOwnerRepo(repoUrl);
            var url = $"https://api.github.com/repos/{owner}/{repo}";
            var result = await ApiGetJson<JsonElement>(url, accessToken ?? "");
            var ok = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("id", out _);
            return ok
                ? (true, "")
                : (false, "Repository not found or inaccessible.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ─────────────────────────────────────────────
    //  REMOTE: API HELPERS
    // ─────────────────────────────────────────────

    private async Task<List<string>> GetApiBranches(string owner, string repo, string accessToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/branches?per_page=100";
        var result = await ApiGetJson<JsonElement>(url, accessToken);
        if (result.ValueKind != JsonValueKind.Array) return new List<string>();
        return result.EnumerateArray()
                     .Select(b => b.GetProperty("name").GetString()!)
                     .Where(b => !string.IsNullOrEmpty(b))
                     .ToList();
    }

    private async Task<string> GetDefaultBranch(string owner, string repo, string accessToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}";
        var result = await ApiGetJson<JsonElement>(url, accessToken);
        return result.ValueKind == JsonValueKind.Object
            ? result.GetProperty("default_branch").GetString() ?? "main"
            : "main";
    }

    private async Task<T> ApiGetJson<T>(string url, string accessToken)
    {
        var client = CreateClient(accessToken);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json)!;
    }

    private HttpClient CreateClient(string accessToken)
    {
        var client = _httpClientFactory.CreateClient("github");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GitLeaker/1.0");
        if (!string.IsNullOrWhiteSpace(accessToken))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>
    /// Parses owner/repo from any of these formats:
    ///   https://github.com/owner/repo
    ///   https://github.com/owner/repo.git
    ///   git@github.com:owner/repo.git
    /// </summary>
    private static (string owner, string repo) ParseOwnerRepo(string repoUrl)
    {
        var url = repoUrl.Trim();

        if (url.StartsWith("git@"))
        {
            // git@github.com:owner/repo.git
            var match = Regex.Match(url, @"git@[^:]+:([^/]+)/(.+?)(?:\.git)?$");
            if (match.Success)
                return (match.Groups[1].Value, match.Groups[2].Value);
        }
        else
        {
            // https://github.com/owner/repo[.git]
            var uri = new Uri(url);
            var parts = uri.AbsolutePath.Trim('/').TrimEnd('/').Split('/');
            if (parts.Length >= 2)
                return (parts[0], parts[1].Replace(".git", ""));
        }

        throw new ArgumentException($"Cannot parse owner/repo from URL: {repoUrl}");
    }

    // ─────────────────────────────────────────────
    //  LOCAL REPO OPERATIONS (unchanged)
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

            var changedLines = await GetChangedLinesLocal(repoPath, hash);
            yield return new GitCommit(hash, author, email, date, branch, message, changedLines);
        }
    }

    private async Task<List<(string FilePath, int LineNumber, string Content)>> GetChangedLinesLocal(
        string repoPath, string commitHash)
    {
        try
        {
            var (diff, _, _) = await RunGitCommandRaw(repoPath, $"diff-tree --no-commit-id -r -p {commitHash}");
            return string.IsNullOrEmpty(diff)
                ? new List<(string, int, string)>()
                : ParseDiff(diff);
        }
        catch
        {
            return new List<(string, int, string)>();
        }
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

        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["GIT_ASKPASS"] = "echo";

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (stdout, stderr, process.ExitCode);
    }
}