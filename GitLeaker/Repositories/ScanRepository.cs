using System.Data;
using Dapper;
using GitLeaker.Data;
using GitLeaker.Models;
using GitLeaker.Repositories.DataRows;
using GitLeaker.Repositories.Interfaces;
using GitLeaker.Repositories.Mappers;

namespace GitLeaker.Repositories;

public class ScanRepository : IScanRepository
{
    private readonly IDbConnectionFactory _factory;
    private readonly ScanMapper _mapper = new();

    public ScanRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
        
    }

    public async Task CreateScanAsync(ScanResult scan)
    {
        using var db = _factory.Create();
        await db.ExecuteAsync("usp_InsertScan",
            new
            {
                scan.ScanId,
                scan.RepoPath,
                scan.RepoUrl,
                scan.IsRemote,
                Status = scan.Status.ToString()
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateScanAsync(ScanResult scan)
    {
        using var db = _factory.Create();
        await db.ExecuteAsync("usp_UpdateScan",
            new
            {
                scan.ScanId,
                Status         = scan.Status.ToString(),
                scan.CommitsScanned,
                scan.FilesScanned,
                scan.CompletedAt,
                scan.Error
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task AddFindingAsync(string scanId, LeakFinding f)
    {
        using var db = _factory.Create();
        await db.ExecuteAsync("usp_InsertFinding",
            new
            {
                ScanId             = scanId,
                f.CommitHash,
                f.Author,
                f.AuthorEmail,
                f.CommitDate,
                f.Branch,
                f.FilePath,
                f.LineNumber,
                f.LineContent,
                f.RedactedContent,
                f.SecretType,
                f.MatchedPattern,
                f.Entropy,
                Risk               = f.Risk.ToString(),
                f.CommitMessage,
                f.RemediationAdvice
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ScanResult?> GetScanAsync(string scanId)
    {
        using var db = _factory.Create();

        await using var multi = await db.QueryMultipleAsync("usp_GetScan",
            new { ScanId = scanId },
            commandType: CommandType.StoredProcedure);

        var scan = await multi.ReadFirstOrDefaultAsync<ScanRow>();
        if (scan is null) return null;

        var findings = (await multi.ReadAsync<FindingRow>()).ToList();

        return _mapper.ToScanResult(scan, findings);
    }

    public async Task<List<ScanResult>> GetAllScansAsync()
    {
        using var db = _factory.Create();

        var rows = await db.QueryAsync<ScanRow>("usp_GetAllScans",
            commandType: CommandType.StoredProcedure);

        return rows.Select(r => _mapper.ToScanResult(r, [])).ToList();
    }

    public async Task<ScanSummary> GetScanSummaryAsync(string scanId)
    {
        using var db = _factory.Create();

        await using var multi = await db.QueryMultipleAsync("usp_GetScanSummary",
            new { ScanId = scanId },
            commandType: CommandType.StoredProcedure);

        var totals       = await multi.ReadFirstOrDefaultAsync<TotalsRow>();
        var byType       = (await multi.ReadAsync<(string SecretType, int Total)>()).ToList();
        var devRows      = (await multi.ReadAsync<DevRow>()).ToList();
        var branchRows   = (await multi.ReadAsync<BranchRow>()).ToList();
        var timelineRows = (await multi.ReadAsync<TimelineRow>()).ToList();
        var recent       = (await multi.ReadAsync<FindingRow>()).ToList();

        return _mapper.ToScanSummary(totals, byType, devRows, branchRows, timelineRows, recent);
    }
}