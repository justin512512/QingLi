using QingLi.Infrastructure.Data;

namespace QingLi.Infrastructure.Tests.Support;

internal sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(string directory, SqliteConnectionFactory factory)
    {
        Directory = directory;
        Factory = factory;
    }

    public string Directory { get; }

    public SqliteConnectionFactory Factory { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QingLi.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var factory = new SqliteConnectionFactory(Path.Combine(directory, "qingli.db"));
        var migration = await new DatabaseMigrator(factory).TryMigrateAsync(CancellationToken.None);
        Assert.True(migration.IsWritable, migration.ErrorMessage);
        return new TestDatabase(directory, factory);
    }

    public ValueTask DisposeAsync()
    {
        System.IO.Directory.Delete(Directory, true);
        return ValueTask.CompletedTask;
    }
}
