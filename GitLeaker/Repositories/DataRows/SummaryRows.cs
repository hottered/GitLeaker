namespace GitLeaker.Repositories.DataRows;

internal record TotalsRow(
    int TotalLeaks, int CriticalCount, int HighCount,
    int MediumCount, int LowCount, int BranchesScanned,
    int DevelopersInvolved, int CommitsScanned, int FilesScanned);

internal record DevRow(
    string Author, string AuthorEmail,
    int TotalLeaks, int CriticalLeaks, int HighLeaks,
    int MediumLeaks, int LowLeaks, decimal RiskScore, DateTime LastLeakDate);

internal record BranchRow(
    string Branch, int TotalLeaks, int CriticalLeaks, decimal RiskScore);

internal record TimelineRow(string Date, int Count, int Critical);