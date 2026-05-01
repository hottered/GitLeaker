namespace GitLeaker.Models;

public record GitCommit(
    string Hash,
    string Author,
    string Email,
    DateTime Date,
    string Branch,
    string Message,
    List<(string FilePath, int LineNumber, string Content)> ChangedLines
);