using GitLeaker.Models;

namespace GitLeaker.Services.Interfaces;

public interface IScannerService
{
    Task<string> StartScanAsync(ScanRequest request);
    Task<ScanResult?> GetScanAsync(string scanId);
    Task<List<ScanResult>> GetAllScansAsync();
}
