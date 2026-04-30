using Dapper;
using Microsoft.Data.SqlClient;

namespace GitLeaker.Data.Migrations;

public static class MigrationRunner
{
    public static async Task RunAsync(string pathToRootOfProject, string connectionString, ILogger logger)
    {
        try
        {
            var masterConn = connectionString.Replace(
                "Database=GitLeakerDb", "Database=master",
                StringComparison.OrdinalIgnoreCase);

            await using (var master = new SqlConnection(masterConn))
            {
                await master.ExecuteAsync("""
                    IF NOT EXISTS (
                        SELECT name FROM sys.databases WHERE name = 'GitLeakerDb'
                    )
                    CREATE DATABASE GitLeakerDb;
                    """);
            }

            await using var db = new SqlConnection(connectionString);
            await db.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '_Migrations')
                CREATE TABLE _Migrations (
                    FileName  NVARCHAR(200) NOT NULL PRIMARY KEY,
                    AppliedAt DATETIME2     NOT NULL DEFAULT GETUTCDATE()
                );
                """);
            var migrationsPath = Path.Combine(pathToRootOfProject, "Data", "Migrations");
            var files = Directory.GetFiles(migrationsPath, "*.sql")
                                 .OrderBy(f => f)
                                 .ToList();
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                var alreadyRan = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM _Migrations WHERE FileName = @fileName",
                    new { fileName });

                if (alreadyRan > 0)
                {
                    logger.LogInformation("⏭️  Skipping migration: {File}", fileName);
                    continue;
                }

                var sql = await File.ReadAllTextAsync(file);
                await db.ExecuteAsync(sql);
                await db.ExecuteAsync(
                    "INSERT INTO _Migrations (FileName) VALUES (@fileName)",
                    new { fileName });

                logger.LogInformation("✅ Applied migration: {File}", fileName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Migration failed — app cannot start.");
            throw;
        }
    }
}