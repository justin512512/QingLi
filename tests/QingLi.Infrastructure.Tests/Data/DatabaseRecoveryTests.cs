using QingLi.Infrastructure.Data;

namespace QingLi.Infrastructure.Tests.Data;

public sealed class DatabaseRecoveryTests : IDisposable
{
    private readonly string _temp = Path.Combine(
        Path.GetTempPath(), "QingLi.Tests", Guid.NewGuid().ToString("N"));

    public DatabaseRecoveryTests() => Directory.CreateDirectory(_temp);

    [Fact]
    public async Task Corrupt_database_is_preserved_and_not_overwritten()
    {
        var path = Path.Combine(_temp, "qingli.db");
        await File.WriteAllTextAsync(path, "not-a-sqlite-database");
        var migrator = new DatabaseMigrator(new SqliteConnectionFactory(path));

        var result = await migrator.TryMigrateAsync(default);

        Assert.False(result.IsWritable);
        Assert.Equal("not-a-sqlite-database", await File.ReadAllTextAsync(path));
        Assert.NotNull(result.PreservedCopyPath);
        Assert.True(File.Exists(result.PreservedCopyPath));
    }

    public void Dispose() => Directory.Delete(_temp, true);
}
