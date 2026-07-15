using System.Security.Cryptography;
using System.Text;
using QingLi.Infrastructure.Updates;

namespace QingLi.Infrastructure.Tests.Updates;

public sealed class ValidatedDataPackageStoreTests
{
    [Fact]
    public async Task ValidPackageBecomesCurrentAfterHashAndSchemaValidation()
    {
        await using var fixture = new StoreFixture();
        var bytes = Encoding.UTF8.GetBytes("{\"version\":2}");
        var manifest = Manifest("2026.07.15", bytes);

        var installed = await fixture.Store.InstallAsync(
            manifest,
            new MemoryStream(bytes),
            async (path, token) => Assert.Equal("{\"version\":2}", await File.ReadAllTextAsync(path, token)),
            default);

        Assert.True(File.Exists(installed));
        Assert.Equal(installed, fixture.Store.ResolvePackagePath("history-today", "bundled.json"));
    }

    [Fact]
    public async Task HashMismatchDoesNotReplaceCurrentPackage()
    {
        await using var fixture = new StoreFixture();
        var oldBytes = Encoding.UTF8.GetBytes("old");
        await fixture.Store.InstallAsync(Manifest("2026.07.14", oldBytes), new MemoryStream(oldBytes), Valid, default);
        var oldPath = fixture.Store.ResolvePackagePath("history-today", "bundled.json");
        var newBytes = Encoding.UTF8.GetBytes("new");
        var invalid = Manifest("2026.07.15", newBytes) with { Sha256 = new string('0', 64) };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            fixture.Store.InstallAsync(invalid, new MemoryStream(newBytes), Valid, default));

        Assert.Equal(oldPath, fixture.Store.ResolvePackagePath("history-today", "bundled.json"));
        Assert.Equal("old", await File.ReadAllTextAsync(oldPath));
    }

    [Fact]
    public async Task SchemaFailureAndOlderVersionKeepPreviousPackage()
    {
        await using var fixture = new StoreFixture();
        var current = Encoding.UTF8.GetBytes("current");
        await fixture.Store.InstallAsync(Manifest("2026.07.15", current), new MemoryStream(current), Valid, default);
        var currentPath = fixture.Store.ResolvePackagePath("history-today", "bundled.json");
        var invalid = Encoding.UTF8.GetBytes("invalid");

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.InstallAsync(
            Manifest("2026.07.16", invalid),
            new MemoryStream(invalid),
            (_, _) => throw new InvalidDataException("bad schema"),
            default));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.InstallAsync(
            Manifest("2026.07.14", invalid), new MemoryStream(invalid), Valid, default));

        Assert.Equal(currentPath, fixture.Store.ResolvePackagePath("history-today", "bundled.json"));
    }

    [Fact]
    public void MissingOrDamagedPointerFallsBackToBundledPackage()
    {
        using var fixture = new StoreFixture();
        Directory.CreateDirectory(fixture.Directory);
        var bundled = Path.Combine(fixture.Directory, "bundled.json");
        File.WriteAllText(bundled, "bundled");
        Directory.CreateDirectory(Path.Combine(fixture.Directory, "history-today"));
        File.WriteAllText(Path.Combine(fixture.Directory, "history-today", "current.json"), "not-json");

        Assert.Equal(bundled, fixture.Store.ResolvePackagePath("history-today", bundled));
    }

    private static Task Valid(string path, CancellationToken cancellationToken) => Task.CompletedTask;

    private static DataPackageManifest Manifest(string version, byte[] bytes) => new(
        "history-today",
        version,
        "https://updates.example.test/history.json",
        Convert.ToHexString(SHA256.HashData(bytes)));

    private sealed class StoreFixture : IDisposable, IAsyncDisposable
    {
        public StoreFixture()
        {
            Directory = Path.Combine(Path.GetTempPath(), "QingLi.Tests", Guid.NewGuid().ToString("N"));
            Store = new ValidatedDataPackageStore(Directory);
        }

        public string Directory { get; }
        public ValidatedDataPackageStore Store { get; }
        public void Dispose() { if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
