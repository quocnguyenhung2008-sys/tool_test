using Microsoft.Data.Sqlite;
using ModernSalesApp.Core;

namespace ModernSalesApp.Data;

public sealed class DbConnectionFactory
{
    public SqliteConnection CreateConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.EffectiveDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        return conn;
    }
}
