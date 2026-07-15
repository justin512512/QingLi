namespace QingLi.Core.History;

public interface IHistoryTodayProvider
{
    IReadOnlyList<HistoryTodayEntry> GetEntries(DateOnly date);
}
