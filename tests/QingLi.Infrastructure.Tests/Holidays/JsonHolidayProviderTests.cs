using QingLi.Core.Holidays;
using QingLi.Infrastructure.Holidays;

namespace QingLi.Infrastructure.Tests.Holidays;

public sealed class JsonHolidayProviderTests
{
    [Fact]
    public async Task Reads_holiday_and_makeup_workday()
    {
        var provider = new JsonHolidayProvider();

        var package = await provider.ReadAsync(GetHolidaySourceAssetPath());

        Assert.Equal("CN", package.Country);
        Assert.Equal(2026, package.Year);
        Assert.Equal("国务院办公厅关于2026年部分节假日安排的通知", package.SourceTitle);
        Assert.Equal(new DateOnly(2025, 11, 4), package.PublishedAt);
        Assert.Contains(package.Days, x => x.Name == "国庆节" && !x.IsWorkday);
        Assert.Contains(package.Days, x => x.IsWorkday);
    }

    [Fact]
    public async Task Package_contains_unique_2026_dates_and_all_official_ranges()
    {
        var provider = new JsonHolidayProvider();

        var package = await provider.ReadAsync(GetHolidaySourceAssetPath());
        var uniqueDates = package.Days.Select(x => x.Date).Distinct().Count();
        var makeupWorkdays = package.Days.Where(x => x.IsWorkday).Select(x => x.Date).Order().ToArray();

        Assert.Equal(2026, package.Year);
        Assert.Equal(39, package.Days.Count);
        Assert.Equal(uniqueDates, package.Days.Count);

        AssertHolidayRange(package, "元旦", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3));
        AssertHolidayRange(package, "春节", new DateOnly(2026, 2, 15), new DateOnly(2026, 2, 23));
        AssertHolidayRange(package, "清明节", new DateOnly(2026, 4, 4), new DateOnly(2026, 4, 6));
        AssertHolidayRange(package, "劳动节", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));
        AssertHolidayRange(package, "端午节", new DateOnly(2026, 6, 19), new DateOnly(2026, 6, 21));
        AssertHolidayRange(package, "中秋节", new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 27));
        AssertHolidayRange(package, "国庆节", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7));

        Assert.Equal(
            [
                new DateOnly(2026, 1, 4),
                new DateOnly(2026, 2, 14),
                new DateOnly(2026, 2, 28),
                new DateOnly(2026, 5, 9),
                new DateOnly(2026, 9, 20),
                new DateOnly(2026, 10, 10)
            ],
            makeupWorkdays);
    }

    [Fact]
    public async Task Windows_build_output_contains_holiday_asset_readable_by_provider()
    {
        var root = GetRepositoryRoot();
        var assetPath = Path.Combine(
            root,
            "src",
            "QingLi.Windows",
            "bin",
            "Debug",
            "net8.0-windows",
            "Assets",
            "Holidays",
            "cn-2026.json");

        Assert.True(File.Exists(assetPath), assetPath);

        var provider = new JsonHolidayProvider();
        var package = await provider.ReadAsync(assetPath);

        Assert.Equal("CN", package.Country);
        Assert.Contains(package.Days, x => x.Date == new DateOnly(2026, 10, 1) && x.Name == "国庆节");
    }

    private static string GetHolidaySourceAssetPath()
    {
        return Path.Combine(
            GetRepositoryRoot(),
            "src",
            "QingLi.Windows",
            "Assets",
            "Holidays",
            "cn-2026.json");
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
    }

    private static void AssertHolidayRange(
        HolidayPackage package,
        string name,
        DateOnly start,
        DateOnly end)
    {
        var expected = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
            .Select(offset => start.AddDays(offset))
            .ToArray();

        var actual = package.Days
            .Where(x => x.Name == name && !x.IsWorkday)
            .Select(x => x.Date)
            .Order()
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
