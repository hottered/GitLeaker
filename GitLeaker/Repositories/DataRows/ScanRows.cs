namespace GitLeaker.Repositories.DataRows;

internal record ScanRow(
    string ScanId, string RepoPath, string? RepoUrl, bool IsRemote,
    string Status, int CommitsScanned, int FilesScanned,
    DateTime StartedAt, DateTime? CompletedAt, string? Error);

internal record FindingRow(
    int Id, string ScanId,
    string CommitHash, string Author, string AuthorEmail,
    DateTime CommitDate, string Branch, string FilePath,
    int LineNumber, string? LineContent, string? RedactedContent,
    string SecretType, string? MatchedPattern, double Entropy, string Risk,
    string? CommitMessage, string? RemediationAdvice);