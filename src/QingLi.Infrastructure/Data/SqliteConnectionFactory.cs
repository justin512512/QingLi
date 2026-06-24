using Microsoft.Data.Sqlite;

namespace QingLi.Infrastructure.Data;

public sealed class SqliteConnectionFactory
{
    public SqliteConnectionFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    public SqliteConnection Create(bool readOnly = false)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = false
        };

        return new SqliteConnection(builder.ToString());
    }
}
