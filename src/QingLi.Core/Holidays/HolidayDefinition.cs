namespace QingLi.Core.Holidays;

public sealed record HolidayDefinition(DateOnly Date, string Name, bool IsWorkday);

public sealed record HolidayPackage(
    string Country,
    int Year,
    string Version,
    string SourceUrl,
    string SourceTitle,
    DateOnly PublishedAt,
    IReadOnlyList<HolidayDefinition> Days);
