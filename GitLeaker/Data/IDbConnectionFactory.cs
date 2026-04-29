using System.Data;

namespace GitLeaker.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}