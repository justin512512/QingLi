using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupLayoutStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"QingLi.Windows.Tests.{Guid.NewGuid():N}");
    private readonly string _path;

    public CalendarPopupLayoutStoreTests()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "calendar-popup-layout.json");
    }

    [Fact]
    public async Task SaveAndLoadRoundTripLayout()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var expected = new CalendarPopupLayout(120.5, 80.25, 1040, 520, true);

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task LoadReturnsNullWhenFileIsMissing()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);

        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Null(actual);
    }

    [Fact]
    public async Task LoadReturnsNullForCorruptJson()
    {
        await File.WriteAllTextAsync(_path, "{not-json");
        var store = new JsonCalendarPopupLayoutStore(_path);

        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Null(actual);
    }

    [Theory]
    [InlineData("{\"Left\":1e400,\"Top\":20,\"Width\":800,\"Height\":500,\"IsCustomized\":true}")]
    [InlineData("{\"Left\":10,\"Top\":1e400,\"Width\":800,\"Height\":500,\"IsCustomized\":true}")]
    [InlineData("{\"Left\":10,\"Top\":20,\"Width\":759.99,\"Height\":500,\"IsCustomized\":true}")]
    [InlineData("{\"Left\":10,\"Top\":20,\"Width\":800,\"Height\":419.99,\"IsCustomized\":true}")]
    public async Task LoadReturnsNullForInvalidLayout(string json)
    {
        await File.WriteAllTextAsync(_path, json);
        var store = new JsonCalendarPopupLayoutStore(_path);

        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Null(actual);
    }

    [Theory]
    [InlineData(double.NaN, 20, 800, 500)]
    [InlineData(10, double.PositiveInfinity, 800, 500)]
    [InlineData(10, 20, double.NegativeInfinity, 500)]
    [InlineData(10, 20, 759.99, 500)]
    [InlineData(10, 20, 800, 419.99)]
    public async Task SaveRejectsInvalidLayout(double left, double top, double width, double height)
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var layout = new CalendarPopupLayout(left, top, width, height, true);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync(layout, CancellationToken.None));

        Assert.False(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public async Task SaveAtomicallyReplacesExistingLayoutAndRemovesTempFile()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var original = new CalendarPopupLayout(10, 20, 800, 500, false);
        var replacement = new CalendarPopupLayout(30, 40, 900, 600, true);
        await store.SaveAsync(original, CancellationToken.None);

        await store.SaveAsync(replacement, CancellationToken.None);

        Assert.Equal(replacement, await store.LoadAsync(CancellationToken.None));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public async Task ClearDeletesLayoutAndStaleTempButPreservesSiblingFiles()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var siblingPath = Path.Combine(_directory, "keep.json");
        await File.WriteAllTextAsync(_path, "layout");
        await File.WriteAllTextAsync(_path + ".tmp", "stale");
        await File.WriteAllTextAsync(siblingPath, "keep");

        await store.ClearAsync(CancellationToken.None);

        Assert.False(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
        Assert.True(File.Exists(siblingPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
