using QingLi.Core.History;
using QingLi.Infrastructure.History;

namespace QingLi.Infrastructure.Tests.History;

public sealed class JsonHistoryTodayProviderTests
{
    [Fact]
    public async Task BundledPackageContainsAllCalendarDaysAndValidSources()
    {
        var provider = await JsonHistoryTodayProvider.LoadAsync(GetSourceAssetPath());

        Assert.Equal(366, provider.DayCount);
        foreach (var date in EachDateOfLeapYear(2024))
        {
            var entries = provider.GetEntries(date);
            Assert.InRange(entries.Count, 0, 10);
            Assert.All(entries, entry =>
            {
                Assert.NotEqual(0, entry.Year);
                Assert.False(string.IsNullOrWhiteSpace(entry.Summary));
                Assert.False(string.IsNullOrWhiteSpace(entry.SourceName));
                Assert.True(Uri.TryCreate(entry.SourceUrl, UriKind.Absolute, out var uri));
                Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
            });
        }
    }

    [Fact]
    public async Task GetEntriesUsesMonthAndDayAndOrdersNewestFirst()
    {
        const string json = """
            {
              "version": "test",
              "generatedAt": "2026-07-15T00:00:00Z",
              "license": "CC BY-SA 4.0",
              "sourceName": "Wikipedia",
              "sourceUrl": "https://zh.wikipedia.org/",
              "days": {
                "07-15": [
                  { "year": 1900, "summary": "较早事件", "sourceName": "Wikipedia", "sourceUrl": "https://zh.wikipedia.org/wiki/A" },
                  { "year": 2000, "summary": "较新事件", "sourceName": "Wikipedia", "sourceUrl": "https://zh.wikipedia.org/wiki/B" }
                ]
              }
            }
            """;
        var path = await WriteTemporaryFileAsync(json);

        try
        {
            IHistoryTodayProvider provider = await JsonHistoryTodayProvider.LoadAsync(path, requireCompleteYear: false);

            var actual = provider.GetEntries(new DateOnly(2030, 7, 15));

            Assert.Equal([2000, 1900], actual.Select(entry => entry.Year));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadRejectsInvalidOrIncompletePackages()
    {
        const string json = """
            {
              "version": "test",
              "generatedAt": "2026-07-15T00:00:00Z",
              "license": "CC BY-SA 4.0",
              "sourceName": "Wikipedia",
              "sourceUrl": "https://zh.wikipedia.org/",
              "days": {
                "07-15": [
                  { "year": 2000, "summary": "事件", "sourceName": "Wikipedia", "sourceUrl": "http://example.com/not-secure" }
                ]
              }
            }
            """;
        var path = await WriteTemporaryFileAsync(json);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => JsonHistoryTodayProvider.LoadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static IEnumerable<DateOnly> EachDateOfLeapYear(int year)
    {
        var first = new DateOnly(year, 1, 1);
        return Enumerable.Range(0, 366).Select(first.AddDays);
    }

    private static string GetSourceAssetPath() => Path.Combine(
        GetRepositoryRoot(),
        "src",
        "QingLi.Windows",
        "Assets",
        "History",
        "history-today.zh-CN.json");

    private static string GetRepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    private static async Task<string> WriteTemporaryFileAsync(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"qingli-history-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, contents);
        return path;
    }
}
