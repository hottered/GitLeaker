using GitLeaker.Models;

namespace GitLeaker.Services.Interfaces;

public interface IGitService
{
    // ── LOCAL ──────────────────────────────────────────────────────────────
    Task<bool> IsGitRepo(string path);
    Task<List<string>> GetBranches(string repoPath);
    Task<string> GetCurrentBranch(string repoPath);
    Task<IAsyncEnumerable<GitCommit>> GetCommitsAsync(
        string repoPath,
        string? branch = null,
        int? daysBack = null,
        bool allBranches = false);
 
    // ── REMOTE (API-based ) ────────────────────────────────────
    Task<IAsyncEnumerable<GitCommit>> GetCommitsFromApiAsync(
        string repoUrl,
        string accessToken,
        string? branch = null,
        int? daysBack = null,
        bool allBranches = false);
 
    Task<(bool ok, string error)> ValidateRemoteUrl(
        string repoUrl,
        string? accessToken,
        RepoProvider provider = RepoProvider.Auto);
}