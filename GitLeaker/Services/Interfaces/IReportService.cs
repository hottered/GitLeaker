using GitLeaker.Models;

namespace GitLeaker.Services.Interfaces;

public interface IReportService
{
    Task<ScanSummary> GenerateSummaryAsync(string scanId);
    ScanSummary GenerateSummary(ScanResult scan); // kept for unit tests / in-memory fallback
}