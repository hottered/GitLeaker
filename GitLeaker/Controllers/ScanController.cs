using GitLeaker.Enums;
using GitLeaker.Models;
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
        _report  = report;
        _git     = git;
    }

    /// <summary>
    /// Start a scan. Accepts either a local path OR a remote URL.
    /// Local:  { "repoPath": "/home/user/my-project" }
    /// Remote: { "repoUrl": "https://github.com/myorg/repo", "accessToken": "ghp_xxxx" }
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartScan([FromBody] ScanRequest request)
    {
        var scanId = await _scanner.StartScanAsync(request);
        return Ok(new
        {
            scanId,
            mode    = request.IsRemote ? "remote" : "local",
            message = request.IsRemote
                ? $"Scanning {request.RepoUrl}..."
                : "Scan started successfully."
        });
    }

    /// <summary>Validate a remote repo URL before starting a full scan.</summary>
    [HttpPost("validate-remote")]
    public async Task<IActionResult> ValidateRemote([FromBody] ValidateRemoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepoUrl))
            return BadRequest(new { error = "repoUrl is required." });

        var ok = await _git.ValidateRemoteUrl(
            request.RepoUrl,
            request.AccessToken);

        return ok
            ? Ok(new
            {
                reachable = true,
                error     = "The project exist on the remote repository",
                provider  = DetectProviderName(request.RepoUrl)
            })
            : BadRequest(new
            {
                reachable = false,
                error     = "Could not validate remote repository.",
                provider  = DetectProviderName(request.RepoUrl)
            });
    }

    /// <summary>Poll scan status — call every 1-2s while Status == "Running"</summary>
    [HttpGet("{scanId}/status")]
    public async Task<IActionResult> GetStatus(string scanId)
    {
        var scan = await _scanner.GetScanAsync(scanId);
        if (scan == null) return NotFound();

        return Ok(new
        {
            scanId         = scan.ScanId,
            status         = scan.Status.ToString(),
            isRemote       = scan.IsRemote,
            repoUrl        = scan.RepoUrl,
            commitsScanned = scan.CommitsScanned,
            filesScanned   = scan.FilesScanned,
            startedAt      = scan.StartedAt,
            completedAt    = scan.CompletedAt,
            error          = scan.Error
        });
    }

    /// <summary>Get full results with summary analytics — call after status == "Completed"</summary>
    [HttpGet("{scanId}/results")]
    public async Task<IActionResult> GetResults(string scanId)
    {
        var scan = await _scanner.GetScanAsync(scanId);
        if (scan == null) return NotFound();

        if (scan.Status == ScanStatus.Running)
            return Ok(new { status = "running", message = "Scan still in progress." });

        var summary = await _report.GenerateSummaryAsync(scanId);

        return Ok(new
        {
            scanId      = scan.ScanId,
            status      = scan.Status.ToString(),
            repoPath    = scan.RepoPath,
            repoUrl     = scan.RepoUrl,
            isRemote    = scan.IsRemote,
            startedAt   = scan.StartedAt,
            completedAt = scan.CompletedAt,
            summary
        });
    }

    /// <summary>Get paginated, filterable findings for a scan</summary>
    [HttpGet("{scanId}/findings")]
    public async Task<IActionResult> GetFindings(
        string scanId,
        [FromQuery] string? author     = null,
        [FromQuery] string? branch     = null,
        [FromQuery] string? riskLevel  = null,
        [FromQuery] string? secretType = null,
        [FromQuery] int page           = 1,
        [FromQuery] int pageSize       = 50)
    {
        var scan = await _scanner.GetScanAsync(scanId);
        if (scan == null) return NotFound();

        // Findings come from DB via GetScanAsync — filter in memory on the already-paged set
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
    public async Task<IActionResult> GetAllScans()
    {
        var scans = await _scanner.GetAllScansAsync();

        return Ok(scans.Select(s => new
        {
            s.ScanId,
            s.RepoPath,
            s.RepoUrl,
            s.IsRemote,
            s.Status,
            s.CommitsScanned,
            s.FilesScanned,
            s.StartedAt,
            s.CompletedAt
        }));
    }

    /// <summary>Mark a finding as remediated</summary>
    [HttpPatch("{scanId}/findings/{findingId}/revoke")]
    public async Task<IActionResult> MarkRevoked(string scanId, int findingId)
    {
        // TODO: add MarkRevokedAsync(scanId, findingId) to IScanRepository
        // and a matching usp_MarkFindingRevoked stored procedure + IsRevoked column
        return StatusCode(501, new { error = "Not implemented yet — needs IsRevoked column and stored procedure." });
    }

    private static string DetectProviderName(string url) => url.ToLower() switch
    {
        var u when u.Contains("github")    => "GitHub",
        var u when u.Contains("gitlab")    => "GitLab",
        var u when u.Contains("bitbucket") => "Bitbucket",
        var u when u.Contains("dev.azure") || u.Contains("visualstudio") => "Azure DevOps",
        _ => "Generic Git"
    };
}

public record ValidateRemoteRequest(
    string RepoUrl,
    string? AccessToken);