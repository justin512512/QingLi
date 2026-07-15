namespace QingLi.Core.History;

public sealed record HistoryTodayEntry(
    int Year,
    string Summary,
    string SourceName,
    string SourceUrl);
