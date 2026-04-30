using GitLeaker.Models;

namespace GitLeaker.Repositories.Interfaces;

public interface IScanRepository
{
    Task CreateScanAsync(ScanResult scan);
    Task UpdateScanAsync(ScanResult scan);
    Task AddFindingAsync(string scanId, LeakFinding finding);
    Task<ScanResult?> GetScanAsync(string scanId);
    Task<List<ScanResult>> GetAllScansAsync();
    Task<ScanSummary> GetScanSummaryAsync(string scanId);  // replaces ReportService for DB scans
}