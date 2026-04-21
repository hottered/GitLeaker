using GitLeaker.Models;

namespace GitLeaker.Services.Interfaces;

public interface IScannerService
{
    Task<string> StartScanAsync(ScanRequest request);

    ScanResult? GetScan(string scanId);

    List<ScanResult> GetAllScans();
}
