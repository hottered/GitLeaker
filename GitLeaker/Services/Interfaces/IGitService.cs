using GitLeaker.Models;

namespace GitLeaker.Services.Interfaces;

public interface IGitService
{
    Task<(string path, string error)> CloneRemoteRepo(
        string repoUrl,
        string? accessToken,
        RepoProvider provider = RepoProvider.Auto);

    Task<(bool ok, string error)> ValidateRemoteUrl(
        string repoUrl,
        string? accessToken,
        RepoProvider provider = RepoProvider.Auto);

    void CleanupTempDir(string path);

    // Local repo operations
    Task<bool> IsGitRepo(string path);

    Task<List<string>> GetBranches(string repoPath);

    Task<string> GetCurrentBranch(string repoPath);

    Task<IAsyncEnumerable<GitCommit>> GetCommitsAsync(
        string repoPath,
        string? branch = null,
        int? daysBack = null,
        bool allBranches = false);
}