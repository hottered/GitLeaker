using System.Data;
using Microsoft.Data.SqlClient;

namespace GitLeaker.Data;

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("DefaultConnection string is missing.");
    }

    public IDbConnection Create()
        => new SqlConnection(_connectionString);
}