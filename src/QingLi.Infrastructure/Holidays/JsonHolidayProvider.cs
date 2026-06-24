using System.Text.Json;
using System.Text.Json.Serialization;
using QingLi.Core.Holidays;

namespace QingLi.Infrastructure.Holidays;

public sealed class JsonHolidayProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<HolidayPackage> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<JsonHolidayPackage>(
            stream,
            SerializerOptions,
            cancellationToken);

        if (document is null)
        {
            throw new InvalidDataException("Holiday JSON document is empty.");
        }

        return new HolidayPackage(
            document.Country,
            document.Year,
            document.Version,
            document.SourceUrl,
            document.SourceTitle,
            document.PublishedAt,
            document.Days.Select(day => new HolidayDefinition(day.Date, day.Name, day.IsWorkday)).ToArray());
    }

    private sealed class JsonHolidayPackage
    {
        [JsonPropertyName("country")]
        public required string Country { get; init; }

        [JsonPropertyName("year")]
        public required int Year { get; init; }

        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("sourceUrl")]
        public required string SourceUrl { get; init; }

        [JsonPropertyName("sourceTitle")]
        public required string SourceTitle { get; init; }

        [JsonPropertyName("publishedAt")]
        public required DateOnly PublishedAt { get; init; }

        [JsonPropertyName("days")]
        public required JsonHolidayDay[] Days { get; init; }
    }

    private sealed class JsonHolidayDay
    {
        [JsonPropertyName("date")]
        public required DateOnly Date { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("isWorkday")]
        public required bool IsWorkday { get; init; }
    }
}
