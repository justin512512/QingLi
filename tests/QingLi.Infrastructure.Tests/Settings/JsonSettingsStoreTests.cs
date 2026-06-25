using QingLi.Core.Settings;
using QingLi.Infrastructure.Settings;

namespace QingLi.Infrastructure.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _temp = Path.Combine(
        Path.GetTempPath(), "QingLi.Tests", Guid.NewGuid().ToString("N"));

    public JsonSettingsStoreTests() => Directory.CreateDirectory(_temp);

    [Fact]
    public async Task Missing_settings_return_safe_defaults()
    {
        var store = new JsonSettingsStore(Path.Combine(_temp, "settings.json"));

        var settings = await store.LoadAsync(default);

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(DayOfWeek.Monday, settings.FirstDayOfWeek);
        Assert.False(settings.StartWithWindows);
    }

    [Fact]
    public async Task Saved_settings_round_trip()
    {
        var store = new JsonSettingsStore(Path.Combine(_temp, "settings.json"));
        var expected = AppSettings.Default with
        {
            Theme = AppTheme.Dark,
            FirstDayOfWeek = DayOfWeek.Sunday,
            StartWithWindows = true
        };

        await store.SaveAsync(expected, default);

        Assert.Equal(expected, await store.LoadAsync(default));
    }

    [Fact]
    public async Task Corrupt_settings_are_preserved_and_defaults_returned()
    {
        var path = Path.Combine(_temp, "settings.json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        var store = new JsonSettingsStore(path);

        var actual = await store.LoadAsync(default);

        Assert.Equal(AppSettings.Default, actual);
        Assert.Equal("{ invalid json", await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(_temp, "*.corrupt-copy"));
    }

    public void Dispose() => Directory.Delete(_temp, true);
}
