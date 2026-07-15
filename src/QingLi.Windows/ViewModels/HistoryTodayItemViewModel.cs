using QingLi.Core.History;

namespace QingLi.Windows.ViewModels;

public sealed class HistoryTodayItemViewModel(HistoryTodayEntry entry)
{
    public int Year { get; } = entry.Year;
    public string Summary { get; } = entry.Summary;
    public string SourceName { get; } = entry.SourceName;
    public string SourceUrl { get; } = entry.SourceUrl;
}
