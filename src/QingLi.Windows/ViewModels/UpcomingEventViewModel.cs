using System.Globalization;
using QingLi.Core.Upcoming;

namespace QingLi.Windows.ViewModels;

public sealed class UpcomingEventViewModel(UpcomingEvent value)
{
    public DateOnly Date { get; } = value.Date;
    public string DateText { get; } = value.Date.ToString("M月d日", CultureInfo.InvariantCulture);
    public int DaysRemaining { get; } = value.DaysRemaining;
    public string CountdownText { get; } = value.DaysRemaining == 0 ? "今天" : $"还有{value.DaysRemaining}天";
    public UpcomingEventKind Kind { get; } = value.Kind;
    public string Title { get; } = value.Title;
    public bool? IsRestDay { get; } = value.IsRestDay;
    public Guid? SubjectId { get; } = value.SubjectId;
}
