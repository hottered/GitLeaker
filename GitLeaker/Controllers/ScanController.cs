using GitLeaker.Models;
using GitLeaker.Services;
using GitLeaker.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GitLeaker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScanController : ControllerBase
{
    private readonly IScannerService _scanner;
    private readonly IReportService _report;
    private readonly IGitService _git;
 
    public ScanController(
        IScannerService scanner, 
        IReportService report, 
        IGitService git)
    {
        _scanner = scanner;
        _report = report;
        _git = git;
    }
 
    /// <summary>
    /// Start a scan. Accepts either a local path OR a remote URL.
    ///
    /// Local example:
    ///   { "repoPath": "/home/user/my-project" }
    ///
    /// Remote public example:
    ///   { "repoUrl": "https://github.com/torvalds/linux" }
    ///
    /// Remote private example:
    ///   { "repoUrl": "https://github.com/myorg/private-repo", "accessToken": "ghp_xxxx" }
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartScan([FromBody] ScanRequest request)
    {
        // if (string.IsNullOrWhiteSpace(request.RepoPath) &&
        //     string.IsNullOrWhiteSpace(request.RepoUrl))
        // {
        //     return BadRequest(new
        //     {
        //         error = "Provide either 'repoPath' (local directory) or 'repoUrl' (remote git URL)."
        //     });
        // }
 
        // try
        // {
            var scanId = await _scanner.StartScanAsync(request);
            return Ok(new
            {
                scanId,
                mode = request.IsRemote ? "remote" : "local",
                message = request.IsRemote
                    ? $"Cloning and scanning {request.RepoUrl}..."
                    : "Scan started successfully."
            });
        // }
        // catch (ArgumentException ex)
        // {
        //     return BadRequest(new { error = ex.Message });
        // }
    }
 
    /// <summary>
    /// Validate a remote repo URL without starting a full scan.
    /// Useful to check if a URL is reachable and a token is valid before committing to a long clone.
    /// </summary>
    [HttpPost("validate-remote")]
    public async Task<IActionResult> ValidateRemote([FromBody] ValidateRemoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepoUrl))
            return BadRequest(new { error = "repoUrl is required." });
 
        var (ok, error) = await _git.ValidateRemoteUrl(
            request.RepoUrl,
            request.AccessToken,
            request.Provider);
 
        return Ok(new
        {
            reachable = ok,
            error = ok ? null : error,
            provider = DetectProviderName(request.RepoUrl)
        });
    }
 
    /// <summary>Poll scan status — call this every 1-2s while Status == "Running"</summary>
    [HttpGet("{scanId}/status")]
    public IActionResult GetStatus(string scanId)
    {
        var scan = _scanner.GetScan(scanId);
        if (scan == null) return NotFound();
 
        return Ok(new
        {
            scanId = scan.ScanId,
            status = scan.Status.ToString(),
            isRemote = scan.IsRemote,
            repoUrl = scan.RepoUrl,
            commitsScanned = scan.CommitsScanned,
            filesScanned = scan.FilesScanned,
            findingsCount = scan.Findings.Count,
            startedAt = scan.StartedAt,
            completedAt = scan.CompletedAt,
            error = scan.Error
        });
    }
 
    /// <summary>Get full results with summary analytics — call after status == "Completed"</summary>
    [HttpGet("{scanId}/results")]
    public IActionResult GetResults(string scanId)
    {
        var scan = _scanner.GetScan(scanId);
        if (scan == null) return NotFound();
        if (scan.Status == ScanStatus.Running)
            return Ok(new { status = "running", message = "Scan still in progress." });
 
        var summary = _report.GenerateSummary(scan);
        return Ok(new
        {
            scanId = scan.ScanId,
            status = scan.Status.ToString(),
            repoPath = scan.RepoPath,
            repoUrl = scan.RepoUrl,
            isRemote = scan.IsRemote,
            startedAt = scan.StartedAt,
            completedAt = scan.CompletedAt,
            summary
        });
    }
 
    /// <summary>Get paginated, filterable findings for a scan</summary>
    [HttpGet("{scanId}/findings")]
    public IActionResult GetFindings(
        string scanId,
        [FromQuery] string? author = null,
        [FromQuery] string? branch = null,
        [FromQuery] string? riskLevel = null,
        [FromQuery] string? secretType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var scan = _scanner.GetScan(scanId);
        if (scan == null) return NotFound();
 
        var findings = scan.Findings.AsQueryable();
 
        if (!string.IsNullOrEmpty(author))
            findings = findings.Where(f =>
                f.Author.Contains(author, StringComparison.OrdinalIgnoreCase) ||
                f.AuthorEmail.Contains(author, StringComparison.OrdinalIgnoreCase));
 
        if (!string.IsNullOrEmpty(branch))
            findings = findings.Where(f => f.Branch == branch);
 
        if (!string.IsNullOrEmpty(riskLevel) && Enum.TryParse<RiskLevel>(riskLevel, true, out var risk))
            findings = findings.Where(f => f.Risk == risk);
 
        if (!string.IsNullOrEmpty(secretType))
            findings = findings.Where(f =>
                f.SecretType.Contains(secretType, StringComparison.OrdinalIgnoreCase));
 
        var total = findings.Count();
        var paged = findings
            .OrderByDescending(f => f.Risk)
            .ThenByDescending(f => f.CommitDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
 
        return Ok(new { total, page, pageSize, findings = paged });
    }
 
    /// <summary>List all scans</summary>
    [HttpGet]
    public IActionResult GetAllScans()
    {
        var scans = _scanner.GetAllScans().Select(s => new
        {
            s.ScanId,
            s.RepoPath,
            s.RepoUrl,
            s.IsRemote,
            s.Status,
            s.CommitsScanned,
            FindingsCount = s.Findings.Count,
            CriticalCount = s.Findings.Count(f => f.Risk == RiskLevel.Critical),
            s.StartedAt,
            s.CompletedAt
        });
        return Ok(scans);
    }
 
    /// <summary>Mark a finding as remediated (secret rotated / revoked)</summary>
    [HttpPatch("{scanId}/findings/{findingId}/revoke")]
    public IActionResult MarkRevoked(string scanId, string findingId)
    {
        var scan = _scanner.GetScan(scanId);
        if (scan == null) return NotFound();
 
        var finding = scan.Findings.FirstOrDefault(f => f.Id == findingId);
        if (finding == null) return NotFound();
 
        finding.IsRevoked = true;
        return Ok(new { message = "Marked as remediated." });
    }
 
    private static string DetectProviderName(string url) => url.ToLower() switch
    {
        var u when u.Contains("github") => "GitHub",
        var u when u.Contains("gitlab") => "GitLab",
        var u when u.Contains("bitbucket") => "Bitbucket",
        var u when u.Contains("dev.azure") || u.Contains("visualstudio") => "Azure DevOps",
        _ => "Generic Git"
    };
}
public record ValidateRemoteRequest(string RepoUrl, string? AccessToken, RepoProvider Provider = RepoProvider.Auto);

