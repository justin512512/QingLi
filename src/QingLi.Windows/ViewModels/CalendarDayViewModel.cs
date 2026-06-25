using System.Collections.ObjectModel;
using QingLi.Core.Calendars;

namespace QingLi.Windows.ViewModels;

public sealed class CalendarDayViewModel
{
    public CalendarDayViewModel(CalendarDay day)
    {
        Date = day.Date;
        LunarText = day.Lunar.ToString();
        SolarTerm = day.SolarTerm;
        HolidayName = day.Holiday?.Name;
        IsCurrentMonth = day.IsCurrentMonth;
    }

    public DateOnly Date { get; }

    public string LunarText { get; }

    public string? SolarTerm { get; }

    public string? HolidayName { get; }

    public bool IsCurrentMonth { get; }

    public ObservableCollection<string> Birthdays { get; } = [];
}
