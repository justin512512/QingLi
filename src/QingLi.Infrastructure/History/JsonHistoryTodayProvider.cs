using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using QingLi.Core.History;

namespace QingLi.Infrastructure.History;

public sealed class JsonHistoryTodayProvider : IHistoryTodayProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IReadOnlyDictionary<string, IReadOnlyList<HistoryTodayEntry>> _days;

    private JsonHistoryTodayProvider(IReadOnlyDictionary<string, IReadOnlyList<HistoryTodayEntry>> days)
    {
        _days = days;
    }

    public int DayCount => _days.Count;

    public static async Task<JsonHistoryTodayProvider> LoadAsync(
        string path,
        bool requireCompleteYear = true,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var package = await JsonSerializer.DeserializeAsync<JsonHistoryPackage>(
            stream,
            SerializerOptions,
            cancellationToken);

        if (package is null)
        {
            throw new InvalidDataException("History JSON document is empty.");
        }

        ValidateMetadata(package);
        var days = ValidateDays(package.Days, requireCompleteYear);
        return new JsonHistoryTodayProvider(days);
    }

    public IReadOnlyList<HistoryTodayEntry> GetEntries(DateOnly date)
    {
        var key = date.ToString("MM-dd", CultureInfo.InvariantCulture);
        return _days.TryGetValue(key, out var entries) ? entries : Array.Empty<HistoryTodayEntry>();
    }

    private static void ValidateMetadata(JsonHistoryPackage package)
    {
        if (string.IsNullOrWhiteSpace(package.Version)
            || string.IsNullOrWhiteSpace(package.License)
            || string.IsNullOrWhiteSpace(package.SourceName)
            || !IsHttpsUrl(package.SourceUrl))
        {
            throw new InvalidDataException("History package metadata is invalid.");
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<HistoryTodayEntry>> ValidateDays(
        Dictionary<string, JsonHistoryEntry[]> days,
        bool requireCompleteYear)
    {
        if (requireCompleteYear && !ExpectedKeys().SetEquals(days.Keys))
        {
            throw new InvalidDataException("History package must contain all 366 calendar days.");
        }

        var result = new Dictionary<string, IReadOnlyList<HistoryTodayEntry>>(StringComparer.Ordinal);
        foreach (var (key, entries) in days)
        {
            if (!IsCalendarDayKey(key) || entries.Length > 10)
            {
                throw new InvalidDataException($"History day '{key}' is invalid.");
            }

            var mapped = entries.Select(entry =>
            {
                if (entry.Year == 0
                    || string.IsNullOrWhiteSpace(entry.Summary)
                    || string.IsNullOrWhiteSpace(entry.SourceName)
                    || !IsHttpsUrl(entry.SourceUrl))
                {
                    throw new InvalidDataException($"History entry for '{key}' is invalid.");
                }

                return new HistoryTodayEntry(
                    entry.Year,
                    entry.Summary.Trim(),
                    entry.SourceName.Trim(),
                    entry.SourceUrl);
            })
            .OrderByDescending(entry => entry.Year)
            .ToArray();

            result.Add(key, mapped);
        }

        return result;
    }

    private static HashSet<string> ExpectedKeys()
    {
        var first = new DateOnly(2024, 1, 1);
        return Enumerable.Range(0, 366)
            .Select(offset => first.AddDays(offset).ToString("MM-dd", CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsCalendarDayKey(string key)
    {
        return DateOnly.TryParseExact(
            $"2024-{key}",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static bool IsHttpsUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }

    private sealed class JsonHistoryPackage
    {
        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("generatedAt")]
        public required DateTimeOffset GeneratedAt { get; init; }

        [JsonPropertyName("license")]
        public required string License { get; init; }

        [JsonPropertyName("sourceName")]
        public required string SourceName { get; init; }

        [JsonPropertyName("sourceUrl")]
        public required string SourceUrl { get; init; }

        [JsonPropertyName("days")]
        public required Dictionary<string, JsonHistoryEntry[]> Days { get; init; }
    }

    private sealed class JsonHistoryEntry
    {
        [JsonPropertyName("year")]
        public required int Year { get; init; }

        [JsonPropertyName("summary")]
        public required string Summary { get; init; }

        [JsonPropertyName("sourceName")]
        public required string SourceName { get; init; }

        [JsonPropertyName("sourceUrl")]
        public required string SourceUrl { get; init; }
    }
}
