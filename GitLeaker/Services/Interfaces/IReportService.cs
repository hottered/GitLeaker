using GitLeaker.Models;

namespace GitLeaker.Services.Interfaces;

public interface IReportService
{
    ScanSummary GenerateSummary(ScanResult scan);
}